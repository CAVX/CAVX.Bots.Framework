using CAVX.Bots.Framework.Modules.Contexts;
using Discord.Commands;
using Discord.WebSocket;

namespace CAVX.Bots.Framework.Services;

public interface IContextService : IContextMetadata
{
    void AddContext(IContextMetadata botActionMetadata);

    void AddContext(SocketCommandContext context);

    void AddContext(SocketSlashCommand interaction, DiscordSocketClient client);
}