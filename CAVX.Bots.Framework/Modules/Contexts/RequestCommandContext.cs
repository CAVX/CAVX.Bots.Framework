using CAVX.Bots.Framework.Extensions;
using CAVX.Bots.Framework.Models;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Contexts;

public class RequestCommandContext(SocketCommandContext context) : RequestContext
{
    public SocketCommandContext OriginalContext { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public override DiscordSocketClient Client => OriginalContext.Client;
    public override SocketGuild Guild => OriginalContext.Guild;
    public override ISocketMessageChannel Channel => OriginalContext.Channel;
    public override SocketUser User => OriginalContext.User;
    public override SocketUserMessage Message => OriginalContext.Message;

    public override async Task<RestUserMessage> ReplyAsync(EphemeralRule ephemeralRule, string message = null, bool isTts = false, FileAttachment[] attachments = null,
        Embed[] embeds = null, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null,
        MessageComponent components = null, bool hasMentions = false)
    {
        bool hasAttachments = attachments?.Any() == true;

        await GetInitialAsync(true);

        if (!embeds.ExistsWithItems() && embed != null)
            embeds = new[] { embed };

        return await _sendMessageQueueLock.LockAsync(async () =>
            hasAttachments
                ? await Channel.SendFilesAsync(attachments, message, isTts, null, options, allowedMentions, messageReference, components, embeds: embeds)
                : await Channel.SendMessageAsync(message, isTts, null, options, allowedMentions, messageReference, components, embeds: embeds));
    }

    public override async Task UpdateReplyAsync(Action<MessageProperties> propBuilder, RequestOptions options = null)
    {
        await GetInitialAsync(true);
        if (OriginalContext.Message != null)
            await OriginalContext.Message.ModifyAsync(propBuilder);
    }
}