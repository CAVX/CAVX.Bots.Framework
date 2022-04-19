using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Services;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Utilities;
using CAVX.Bots.Framework.Extensions;

namespace CAVX.Bots.Framework.Modules.Contexts
{
    public enum RequestAcknowledgeStatus
    {
        NotAcknowledged,
        Acknowledged,
        AcknowledgeFailed
    }

    public class RequestInteractionContext : RequestContext
    {
        readonly SemaphoreLocker _acknowledgedLock = new();
        RequestAcknowledgeStatus _acknowledgeStatus = RequestAcknowledgeStatus.NotAcknowledged;

        public RequestInteractionContext(SocketInteraction interaction, DiscordSocketClient client) : base()
        {
            OriginalInteraction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public SocketInteraction OriginalInteraction { get; }

        public async Task<RequestAcknowledgeStatus> HadBeenAcknowledgedAsync(RequestAcknowledgeStatus? setIfNotAcknowledged = null, Func<Task> performIfNotAcknowledged = null)
        {
            return await _acknowledgedLock.LockAsync(async () =>
            {
                var previousStatus = _acknowledgeStatus;
                if (previousStatus == RequestAcknowledgeStatus.NotAcknowledged && setIfNotAcknowledged.HasValue)
                    _acknowledgeStatus = setIfNotAcknowledged.Value;
                if (previousStatus == RequestAcknowledgeStatus.NotAcknowledged && performIfNotAcknowledged != null)
                {
                    try
                    {
                        await performIfNotAcknowledged();
                    }
                    catch (HttpException)
                    {
                        _acknowledgeStatus = RequestAcknowledgeStatus.AcknowledgeFailed;
                    }
                }

                return previousStatus;
            });
        }

        public override DiscordSocketClient Client { get; }
        public override SocketGuild Guild => (OriginalInteraction.Channel as IGuildChannel)?.Guild as SocketGuild;
        public override ISocketMessageChannel Channel => OriginalInteraction.Channel;
        public override SocketUser User => OriginalInteraction.User;

        public InteractionType Type => OriginalInteraction.Type;

        public override SocketUserMessage Message
        {
            get
            {
                if (OriginalInteraction is SocketMessageComponent smc)
                    return smc.Message;
                return null;
            }
        }

        public override async Task<RestUserMessage> ReplyAsync(EphemeralRule ephemeralRule, string message = null, bool isTTS = false, FileAttachment[] attachments = null,
            Embed[] embeds = null, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null,
            MessageComponent components = null, bool hasMentions = false)
        {
            if (!embeds.ExistsWithItems() && embed != null)
                embeds = new Embed[] { embed };

            return await _acknowledgedLock.LockAsync(async () =>
            {
                bool initial = await GetInitialAsync(true);
                var previousStatus = _acknowledgeStatus;
                bool hasAttachments = attachments != null && attachments.Any();

                try
                {
                    if (initial && previousStatus == RequestAcknowledgeStatus.NotAcknowledged)
                    {
                        if (hasAttachments)
                            await OriginalInteraction.RespondWithFilesAsync(attachments, message, embeds, isTTS, ephemeralRule.ToEphemeral(), allowedMentions, components, null, options);
                        else
                            await OriginalInteraction.RespondAsync(message, embeds, isTTS, ephemeralRule.ToEphemeral(), allowedMentions, components, null, options);
                        _acknowledgeStatus = RequestAcknowledgeStatus.Acknowledged;

                        try
                        {
                            return await OriginalInteraction.GetOriginalResponseAsync();
                        }
                        catch (HttpException)
                        {
                            await Task.Delay(1000);
                            return await OriginalInteraction.GetOriginalResponseAsync();
                        }
                    }
                    else if (initial && OriginalInteraction is SocketSlashCommand)
                    {
                        if (hasMentions && ephemeralRule == EphemeralRule.Permanent)
                        {
                            await TryDeleteOriginalMessageAsync();

                            return await _sendMessageQueueLock.LockAsync(async () =>
                                hasAttachments
                                    ? await Channel.SendFilesAsync(attachments, message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds) as RestUserMessage
                                    : await Channel.SendMessageAsync(message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds));
                        }
                        else if (!hasAttachments)
                        {
                            try
                            {
                                //Adding new attachments isn't working and the ! is just a stopgap too. 
                                return await OriginalInteraction.ModifyOriginalResponseAsync(mp =>
                                {
                                    if (!mp.Flags.IsSpecified)
                                        mp.Flags = MessageFlags.None;

                                    mp.Content = message;
                                    mp.Embeds = embeds;
                                    mp.AllowedMentions = allowedMentions;
                                    mp.Components = components;
                                    mp.Attachments = attachments;
                                }, options);
                            }
                            catch (HttpException)
                            {
                                await Task.Delay(1000);
                                return await OriginalInteraction.ModifyOriginalResponseAsync(mp =>
                                {
                                    if (!mp.Flags.IsSpecified)
                                        mp.Flags = MessageFlags.None;

                                    mp.Content = message;
                                    mp.Embeds = embeds;
                                    mp.AllowedMentions = allowedMentions;
                                    mp.Components = components;
                                    mp.Attachments = attachments;
                                }, options);
                            }
                        }
                    }

                    if (messageReference == null || ephemeralRule == EphemeralRule.EphemeralOrFail)
                        return await _sendMessageQueueLock.LockAsync(async () =>
                            hasAttachments
                                ? await OriginalInteraction.FollowupWithFilesAsync(attachments, message, embeds, isTTS, ephemeralRule.ToEphemeral(), allowedMentions, components, null, options)
                                : await OriginalInteraction.FollowupAsync(message, embeds, isTTS, ephemeralRule.ToEphemeral(), allowedMentions, components, null, options));
                    else
                        return await _sendMessageQueueLock.LockAsync(async () =>
                            hasAttachments
                                ? await Channel.SendFilesAsync(attachments, message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds) as RestUserMessage
                                : await Channel.SendMessageAsync(message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds));
                }
                catch (TimeoutException te)
                {
                    return await RetryReplyAsync(te, ephemeralRule, message, isTTS, attachments, embeds, null, options, allowedMentions, messageReference, components, initial, hasAttachments);
                }
                catch (HttpException e)
                {
                    return await RetryReplyAsync(e, ephemeralRule, message, isTTS, attachments, embeds, null, options, allowedMentions, messageReference, components, initial, hasAttachments);
                }
            });
        }

        private async Task<RestUserMessage> RetryReplyAsync(Exception ex, EphemeralRule ephemeralRule, string message, bool isTTS, FileAttachment[] attachments,
            Embed[] embeds, Embed embed, RequestOptions options, AllowedMentions allowedMentions, MessageReference messageReference, MessageComponent components,
            bool initial, bool hasAttachments)
        {
            _acknowledgeStatus = RequestAcknowledgeStatus.AcknowledgeFailed;
            Console.WriteLine(ex.ToString());
            await Logger.Instance.LogErrorAsync(this, "RequestInteractionContext.ReplyWithMessageAsync", ex);

            if (initial && OriginalInteraction is SocketSlashCommand)
                await TryDeleteOriginalMessageAsync();

            if (ephemeralRule == EphemeralRule.EphemeralOrFail)
                return await _sendMessageQueueLock.LockAsync(async () =>
                    await Channel.SendMessageAsync("It took me too long to process that, and I don't want to show anyone else! Sorry! Try again!"));
            else
                return await _sendMessageQueueLock.LockAsync(async () =>
                    hasAttachments
                        ? await Channel.SendFilesAsync(attachments, message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds) as RestUserMessage
                        : await Channel.SendMessageAsync(message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds));
        }

        public override async Task UpdateReplyAsync(Action<MessageProperties> propBuilder, RequestOptions options = null)
        {
            await _acknowledgedLock.LockAsync(async () =>
            {
                try
                {
                    bool initial = await GetInitialAsync(true);

                    var previousStatus = _acknowledgeStatus;
                    if (OriginalInteraction is SocketMessageComponent smc && previousStatus == RequestAcknowledgeStatus.NotAcknowledged)
                    {
                        await smc.UpdateAsync(propBuilder);
                        _acknowledgeStatus = RequestAcknowledgeStatus.Acknowledged;
                    }
                    else
                    {
                        if (initial && previousStatus == RequestAcknowledgeStatus.NotAcknowledged)
                        {
                            await OriginalInteraction.DeferAsync(true);
                            _acknowledgeStatus = RequestAcknowledgeStatus.Acknowledged;
                        }

                        var message = await OriginalInteraction.GetOriginalResponseAsync();
                        await message.ModifyAsync(propBuilder);
                    }
                }
                catch (HttpException e)
                {
                    _acknowledgeStatus = RequestAcknowledgeStatus.AcknowledgeFailed;
                    Console.WriteLine(e.ToString());
                    await Logger.Instance.LogErrorAsync(this, "RequestInteractionContext.ReplyWithMessageAsync", e);
                }
            });
        }

        private async Task TryDeleteOriginalMessageAsync()
        {
            if (_acknowledgeStatus == RequestAcknowledgeStatus.NotAcknowledged)
            {
                try
                {
                    await OriginalInteraction.DeferAsync();
                    _acknowledgeStatus = RequestAcknowledgeStatus.Acknowledged;
                }
                catch (HttpException)
                {
                    _acknowledgeStatus = RequestAcknowledgeStatus.AcknowledgeFailed;
                }
            }

            _ = Task.Run(async () =>
            {
                try { await OriginalInteraction?.DeleteOriginalResponseAsync(); }
                catch
                {
                    try
                    {
                        await (await OriginalInteraction?.GetOriginalResponseAsync())?.DeleteAsync();
                    }
                    catch { /*eh*/ }
                }
            });
        }
    }
}
