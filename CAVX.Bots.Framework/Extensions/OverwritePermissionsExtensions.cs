using Discord;

namespace CAVX.Bots.Framework.Extensions
{
    public static class OverwritePermissionsExtensions
    {
        public static bool? ViewChannelPermissionDefined(this OverwritePermissions? overwritePermissions)
        {
            if (!overwritePermissions.HasValue || overwritePermissions.Value.ViewChannel == PermValue.Inherit)
                return null;

            return overwritePermissions.Value.ViewChannel == PermValue.Allow;
        }
    }
}