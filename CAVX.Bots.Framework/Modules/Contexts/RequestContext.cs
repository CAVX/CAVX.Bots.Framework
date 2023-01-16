using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Services;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Utilities;
using CAVX.Bots.Framework.Extensions;
using System.Linq;
using AsyncKeyedLock;

namespace CAVX.Bots.Framework.Modules.Contexts
{
    public abstract class RequestContext
    {
        protected readonly static SemaphoreLocker _sendMessageQueueLock = new();

        readonly SemaphoreLocker _initialLock = new();

        private bool _initial = true;
        public async Task<bool> GetInitialAsync(bool updateAfterTouch)
        {
            return await _initialLock.LockAsync(() =>
            {
                bool val = _initial;
                if (updateAfterTouch)
                    _initial = false;

                return Task.FromResult(val);
            });
        }

        private ulong? _botOwnerId;
        public async Task<ulong> GetBotOwnerIdAsync()
        {
            if (_botOwnerId.HasValue)
                return _botOwnerId.Value;

            var application = await Client.GetApplicationInfoAsync().ConfigureAwait(false);
            _botOwnerId = application.Owner.Id;
            return _botOwnerId.Value;
        }


        public abstract DiscordSocketClient Client { get; }
        public abstract SocketGuild Guild { get; }
        public abstract ISocketMessageChannel Channel { get; }
        public abstract SocketUser User { get; }
        public abstract SocketUserMessage Message { get; }

        public ulong? GetReferenceMessageId() => Message?.Id;

        public Task<RestUserMessage> ReplyAsync(bool ephemeral, string message = null, bool isTTS = false, FileAttachment[] attachments = null, Embed[] embeds = null, Embed embed = null,
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false)
            => ReplyAsync(ephemeral ? EphemeralRule.EphemeralOrFallback : EphemeralRule.Permanent, message, isTTS, attachments, embeds, embed, options, allowedMentions, messageReference, components, hasMentions);

        public abstract Task<RestUserMessage> ReplyAsync(EphemeralRule ephemeralRule, string message = null, bool isTTS = false, FileAttachment[] attachments = null, Embed[] embeds = null, Embed embed = null,
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false);

        public abstract Task UpdateReplyAsync(Action<MessageProperties> propBuilder, RequestOptions options = null);

        public Task ReplyBuilderAsync(IServiceProvider baseServices, IMessageBuilder messageBuilder, bool ephemeral, bool useQueue, ulong? referenceMessageId = null)
            => ReplyBuilderAsync(baseServices, messageBuilder, ephemeral ? EphemeralRule.EphemeralOrFallback : EphemeralRule.Permanent, useQueue, referenceMessageId);
        public async Task ReplyBuilderAsync(IServiceProvider baseServices, IMessageBuilder messageBuilder, EphemeralRule ephemeralRule, bool useQueue, ulong? referenceMessageId = null)
        {
            var messageData = messageBuilder.BuildOutput();

            if (messageData != null && (messageData.Embeds.ExistsWithItems() || messageData.Message != null))
            {
                ephemeralRule = messageBuilder.Result != MessageResultCode.Success && ephemeralRule == EphemeralRule.Permanent ? EphemeralRule.EphemeralOrFallback : ephemeralRule;

                byte[] streamBytes = messageData.ImageStreamBytes;
                using Stream stream = streamBytes == null ? null : new MemoryStream(streamBytes);

                //Getting several messages from the framework - this is my super hacky way around them.
                RestUserMessage message = null;
                int errorCount = 0;
                while (errorCount <= 1)
                {
                    try
                    {
                        FileAttachment[] attachments = stream == null ? null
                            : new FileAttachment[] { new(stream, messageData.ImageFileName, isSpoiler: messageData.ImageIsSpoiler) };

                        message = await ReplyAsync(ephemeralRule, messageData.Message, attachments: attachments, embeds: messageData.Embeds, components: messageData.Components,
                            messageReference: referenceMessageId.HasValue ? new MessageReference(referenceMessageId.Value) : null, hasMentions: messageData.HasMentions).ConfigureAwait(false);

                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        errorCount++;
                        if (errorCount > 1)
                            throw;

                        await Task.Delay(1000);
                    }
                    catch (System.Net.Http.HttpRequestException hre)
                    {
                        if (hre.InnerException == null || hre.InnerException is not IOException)
                            break;

                        errorCount++;
                        if (errorCount > 1)
                            throw;

                        await Task.Delay(1000);
                    }
                }

                if (messageBuilder.DeferredBuilder != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var asyncKeyedLocker = baseServices.GetRequiredService<AsyncKeyedLocker<ulong>>();
                            using (await asyncKeyedLocker.LockAsync(Guild.Id))
                            {
                                await SendDeferredMessage(baseServices, messageBuilder, ephemeralRule, useQueue, message);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            await ReplyAsync(ephemeralRule, "Something went wrong!").ConfigureAwait(false);
                            await Logger.Instance.LogErrorAsync(this, "Inner execution exception", e);
                            return;
                        }
                    });
                }
            }

            async Task SendDeferredMessage(IServiceProvider baseServices, IMessageBuilder messageBuilder, EphemeralRule ephemeralRule, bool useQueue, RestUserMessage message)
            {
                var scope = baseServices.CreateScope();
                var builderInstance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, messageBuilder.DeferredBuilder.InstanceType);
                if (builderInstance != null)
                {
                    messageBuilder.DeferredBuilder.SetInstanceAndProperties(builderInstance, this, message);
                    var innerBuilder = await messageBuilder.DeferredBuilder.GetDeferredMessage();
                    if (innerBuilder != null)
                    {
                        await ReplyBuilderAsync(baseServices, innerBuilder, ephemeralRule, useQueue, message.Id);
                    }
                }
            }
        }
    }
}
