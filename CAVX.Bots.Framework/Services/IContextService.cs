using CAVX.Bots.Framework.Modules.Contexts;
using Discord.Commands;
using Discord.WebSocket;


namespace CAVX.Bots.Framework.Services
{
    public interface IContextService
    {
        public RequestContext Context { get; set; }

        void AddContext(RequestContext context);
        void AddContext(SocketCommandContext context);
        void AddContext(SocketSlashCommand interaction, DiscordSocketClient client);
    }
}
