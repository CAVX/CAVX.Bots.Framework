using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Processing;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IBotAction : IContextMetadata
    {
        EphemeralRule EphemeralRule { get; }
        bool RestrictAccessToGuilds { get; }
        bool ConditionalGuildsOnly { get; }
        ActionAccessRule RequiredAccessRule { get; }

        Task<(bool Success, string Message)> CheckPreconditionsAsync(ActionRunContext runContext);
        IEnumerable<(PropertyInfo Property, T Attribute)> GetParameters<T>() where T : IActionParameterAttribute;
        void Initialize(RequestContext context);
        (bool Success, string Message) IsCommandAllowedInGuild();
        Task RunAsync(ActionRunContext runContext);
        bool ValidateParameters<T>() where T : IActionParameterAttribute;
    }
}