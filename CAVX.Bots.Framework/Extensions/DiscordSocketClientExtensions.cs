using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions
{
    public static class DiscordSocketClientExtensions
    {
        private static ulong? _ownerId;

        public static async Task SendDirectMessageToOwner(this DiscordSocketClient client, string message)
        {
            if (!_ownerId.HasValue)
            {
                var application = await client.GetApplicationInfoAsync().ConfigureAwait(false);
                _ownerId = application.Owner.Id;
            }

            var user = await client.GetUserAsync(_ownerId.Value);
            await user.SendMessageAsync(message);
        }
    }
}