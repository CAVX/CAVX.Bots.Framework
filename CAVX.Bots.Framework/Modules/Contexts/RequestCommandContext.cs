using CAVX.Bots.Framework.Models;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Extensions;

namespace CAVX.Bots.Framework.Modules.Contexts
{
    public class RequestCommandContext : RequestContext
    {
        public RequestCommandContext(SocketCommandContext context) : base()
        {
            OriginalContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        public SocketCommandContext OriginalContext { get; }

        public override DiscordSocketClient Client => OriginalContext.Client;
        public override SocketGuild Guild => OriginalContext.Guild;
        public override ISocketMessageChannel Channel => OriginalContext.Channel;
        public override SocketUser User => OriginalContext.User;
        public override SocketUserMessage Message => OriginalContext.Message;

        public async override Task<RestUserMessage> ReplyAsync(EphemeralRule ephemeralRule, string message = null, bool isTTS = false, FileAttachment[] attachments = null,
            Embed[] embeds = null, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null,
            MessageComponent components = null, bool hasMentions = false)
        {
            bool hasAttachments = attachments != null && attachments.Any();

            await GetInitialAsync(true);

            if (!embeds.ExistsWithItems() && embed != null)
                embeds = new Embed[] { embed };

            return await _sendMessageQueueLock.LockAsync(async () =>
                hasAttachments
                    ? await Channel.SendFilesAsync(attachments, message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds) as RestUserMessage
                    : await Channel.SendMessageAsync(message, isTTS, null, options, allowedMentions, messageReference, components, embeds: embeds));
        }

        public override async Task UpdateReplyAsync(Action<MessageProperties> propBuilder, RequestOptions options = null)
        {
            await GetInitialAsync(true);
            await OriginalContext.Message?.ModifyAsync(propBuilder);
        }
    }
}
