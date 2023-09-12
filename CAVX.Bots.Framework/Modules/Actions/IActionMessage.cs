using Discord.WebSocket;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionMessage : IBotAction
    {
        string CommandName { get; }

        Task FillMessageParametersAsync(SocketMessage message);
    }
}