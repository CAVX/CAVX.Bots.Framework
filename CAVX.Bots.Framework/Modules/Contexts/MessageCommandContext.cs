using Discord.Commands;
using Discord.WebSocket;

namespace CAVX.Bots.Framework.Modules.Contexts;

public class MessageCommandContext(DiscordSocketClient client, SocketUserMessage msg, string input)
    : SocketCommandContext(client, msg)
{
    public string CommandInput { get; } = input;
}