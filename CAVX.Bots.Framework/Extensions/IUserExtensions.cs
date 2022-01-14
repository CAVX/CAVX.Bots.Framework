using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

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
            var nickname = (await guild?.GetUserAsync(user.Id))?.Nickname;
            return string.IsNullOrWhiteSpace(nickname) ? user.Username : nickname;
        }
    }
}
