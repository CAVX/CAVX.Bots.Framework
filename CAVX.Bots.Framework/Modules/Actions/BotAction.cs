using CAVX.Bots.Framework.Extensions;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Processing;
using Discord;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public abstract class BotAction : IBotAction
    {
        public bool UseQueue => GetType().GetCustomAttributes(false).OfType<ActionUseQueueAttribute>().ExistsWithItems();
        public bool SkipDefer => GetType().GetCustomAttributes(false).OfType<ActionNoDeferAttribute>().ExistsWithItems();

        public RequestContext Context { get; set; }

        public void Initialize(RequestContext context)
        {
            Context = context;
        }

        public abstract EphemeralRule EphemeralRule { get; }
        public abstract bool RestrictAccessToGuilds { get; }
        public abstract bool ConditionalGuildsOnly { get; }
        public abstract ActionAccessRule RequiredAccessRule { get; }

        public bool ValidateParameters<T>() where T : IActionParameterAttribute
        {
            //Required check (all we have right now)
            var parameterProperties = GetType().GetProperties().SelectMany(p => p.GetCustomAttributes(false).OfType<T>().Select(a => new { Property = p, Attribute = a })).Where(p => p.Attribute.Required).ToArray();

            return !parameterProperties.Any() || parameterProperties.Select(p => p.Property.GetValue(this)).All(value => value != null);
        }

        public IEnumerable<(PropertyInfo Property, T Attribute)> GetParameters<T>() where T : IActionParameterAttribute
        {
            return GetType().GetProperties().SelectMany(p => p.GetCustomAttributes(false).OfType<T>().Select(a => (Property: p, Attribute: a)));
        }

        public async Task<(bool Success, string Message)> CheckPreconditionsAsync(ActionRunContext runContext)
        {
            if (RequiredAccessRule != null)
            {
                if (RequiredAccessRule.PermissionType == ActionPermissionType.RequireOwner)
                {
                    ulong botOwnerId = await Context.GetBotOwnerIdAsync();
                    if (botOwnerId != Context.User.Id)
                        return (false, "You need to be the bot creator to run that command! Sorry!");
                }
                else if (RequiredAccessRule.PermissionType == ActionPermissionType.RequirePermission && RequiredAccessRule.RequiredPermission.HasValue
                    && !HasCorrectPermissions(Context.User as IGuildUser, RequiredAccessRule.RequiredPermission.Value))
                {
                    return (false, "You don't have the permissions to run that command! Sorry!");
                }
            }

            return await CheckCustomPreconditionsAsync(runContext);
        }

        protected static bool HasCorrectPermissions(IGuildUser user, GuildPermission guildPermission)
        {
            var userPermissions = user.GuildPermissions;

            return userPermissions.Administrator || userPermissions.Has(guildPermission);
        }

        protected abstract Task<(bool Success, string Message)> CheckCustomPreconditionsAsync(ActionRunContext runContext);

        public abstract Task RunAsync(ActionRunContext runContext);

        public (bool Success, string Message) IsCommandAllowedInGuild()
        {
            if (RestrictAccessToGuilds && Context.Channel is IDMChannel)
                return (false, "You can't do that here! Find a server that I'm in, instead!");

            return (true, null);
        }
    }
}