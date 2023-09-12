using Discord;
using Discord.WebSocket;

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
            {
                return ThreadChannelRoleAccess.Private;
            }
            else if (role == null)
            {
                return ThreadChannelRoleAccess.PossibleAccess;
            }
            else
            {
                //First check if a channel is blocked to @everyone, because that makes it "private".
                bool isPrivate = false;
                var everyoneRole = role.Guild.EveryoneRole;
                if (everyoneRole.Id != role.Id)
                {
                    isPrivate = !(threadChannel.ParentChannel.GetPermissionOverwrite(everyoneRole).ViewChannelPermissionDefined() ?? true); //only explicitly blocking the channel to @everyone makes it private. Inherit does not.
                }

                //See if users with the current role can view the threads without the right role access.
                var noAccess = !(threadChannel.ParentChannel.GetPermissionOverwrite(role).ViewChannelPermissionDefined()
                    ?? (!isPrivate && (role.Permissions.ViewChannel || everyoneRole.Permissions.ViewChannel))); //if it's private, you need to explicitly have a channel-based permission defined.

                return noAccess ? ThreadChannelRoleAccess.Forbidden : ThreadChannelRoleAccess.Accessible;
            }
        }
    }
}