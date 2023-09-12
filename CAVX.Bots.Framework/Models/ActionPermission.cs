using Discord;

namespace CAVX.Bots.Framework.Models
{
    public enum ActionPermissionType
    {
        RequireOwner,
        RequirePermission
    }

    public sealed class ActionAccessRule
    {
        public ActionPermissionType PermissionType { get; }
        public GuildPermission? RequiredPermission { get; }

        public static ActionAccessRule CreateWithRequireOwner() => new(ActionPermissionType.RequireOwner);

        public static ActionAccessRule CreateWithRequirePermission(GuildPermission requiredPermission) => new(ActionPermissionType.RequirePermission, requiredPermission);

        private ActionAccessRule(ActionPermissionType permissionType, GuildPermission? requiredPermission = null)
        {
            PermissionType = permissionType;
            RequiredPermission = permissionType == ActionPermissionType.RequirePermission ? requiredPermission : null;
        }
    }
}