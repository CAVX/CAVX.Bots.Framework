using Discord;
using Discord.WebSocket;
using System;

namespace CAVX.Bots.Framework.Extensions
{
    public static class IUserExtensions
    {
        public static string DisplayName(this IUser user, SocketGuild guild)
        {
            var nickname = guild?.GetUser(user.Id)?.Nickname;
            return string.IsNullOrWhiteSpace(nickname) ? user.Username : nickname;
        }

        public static async System.Threading.Tasks.Task<string> DisplayNameAsync(this IUser user, IGuild guild)
        {
            if (user is null)
                throw new ArgumentNullException(nameof(user));

            if (guild is null)
                throw new ArgumentNullException(nameof(guild));

            var nickname = (await guild.GetUserAsync(user.Id))?.Nickname;
            return string.IsNullOrWhiteSpace(nickname) ? user.Username : nickname;
        }
    }
}