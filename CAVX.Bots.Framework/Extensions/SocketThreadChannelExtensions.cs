using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions
{
    public enum ThreadChannelRoleAccess
    {
        Private,
        PossibleAccess,
        Forbidden,
        Accessible
    }

    public static class SocketThreadChannelExtensions
    {
        public static ThreadChannelRoleAccess CheckRoleAccess(this SocketThreadChannel threadChannel, IRole role)
        {
            //Only mods can view private threads.
            if (threadChannel.IsPrivateThread)
                return ThreadChannelRoleAccess.Private;
            else if (role == null)
                return ThreadChannelRoleAccess.PossibleAccess;
            else
            {
                //First check if a channel is blocked to @everyone, because that makes it "private".
                bool isPrivate = false;
                var everyoneRole = role.Guild.EveryoneRole;
                if (everyoneRole.Id != role.Id)
                {
                    isPrivate = !(threadChannel.GetPermissionOverwrite(everyoneRole).ViewChannelPermissionDefined()
                        ?? threadChannel.ParentChannel.GetPermissionOverwrite(everyoneRole).ViewChannelPermissionDefined()
                        ?? (threadChannel.ParentChannel as SocketTextChannel)?.Category?.GetPermissionOverwrite(everyoneRole).ViewChannelPermissionDefined()
                        ?? true);
                }

                //See if users with the current role can view the threads without the right role access.
                var noAccess = !(threadChannel.GetPermissionOverwrite(role).ViewChannelPermissionDefined()
                    ?? threadChannel.ParentChannel.GetPermissionOverwrite(role).ViewChannelPermissionDefined()
                    ?? (threadChannel.ParentChannel as SocketTextChannel)?.Category?.GetPermissionOverwrite(role).ViewChannelPermissionDefined()
                    ?? (!isPrivate && role.Permissions.ViewChannel)); //if it's private, you need to explicitly have a channel setting defined.

                return noAccess ? ThreadChannelRoleAccess.Forbidden : ThreadChannelRoleAccess.Accessible;
            }
        }
    }
}
