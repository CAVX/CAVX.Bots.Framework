using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using Bot.Services;
using Bot.Models;
using Bot.Utilities;

namespace Bot.Modules.Contexts
{
    public abstract class RequestContext
    {
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


        public Task<RestUserMessage> ReplyWithMessageAsync(bool ephemeral, string message = null, bool isTTS = false, Embed[] embeds = null, Embed embed = null,
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false)
            => ReplyWithMessageAsync(ephemeral ? EphemeralRule.EphemeralOrFallback : EphemeralRule.Permanent, message, isTTS, embeds, embed, options, allowedMentions, messageReference, components, hasMentions);
        public Task<RestUserMessage> ReplyWithFileAsync(bool ephemeral, Stream stream, string filename, bool isSpoiler, string message = null, bool isTTS = false, Embed[] embeds = null,
            Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false)
            => ReplyWithFileAsync(ephemeral ? EphemeralRule.EphemeralOrFallback : EphemeralRule.Permanent, stream, filename, isSpoiler, message, isTTS, embeds, embed, options, allowedMentions, messageReference, components, hasMentions);

        public abstract Task<RestUserMessage> ReplyWithMessageAsync(EphemeralRule ephemeralRule, string message = null, bool isTTS = false, Embed[] embeds = null, Embed embed = null, 
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false);
        public abstract Task<RestUserMessage> ReplyWithFileAsync(EphemeralRule ephemeralRule, Stream stream, string filename, bool isSpoiler, string message = null, bool isTTS = false, Embed[] embeds = null,
            Embed embed = null,  RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false);
        public abstract Task UpdateReplyAsync(Action<MessageProperties> propBuilder, RequestOptions options = null);
    }
}
