using Discord.WebSocket;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionUser : IBotAction
    {
        string CommandName { get; }

        Task FillUserParametersAsync(SocketUser user);
    }
}