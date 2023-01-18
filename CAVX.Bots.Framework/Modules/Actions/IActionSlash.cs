using Discord.WebSocket;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using System.Linq;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionSlash : IBotAction, IActionSlashRoot
    {
        Task FillSlashParametersAsync(IEnumerable<SocketSlashCommandDataOption> options);
    }

    public static class IActionSlashExtensions
    {
        public static (string CommandName, IEnumerable<SocketSlashCommandDataOption> Options) GetSelectedSubOption(this IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            var option = options.FirstOrDefault(t => t.Type == Discord.ApplicationCommandOptionType.SubCommand);
            return (option?.Name, option?.Options);
        }

        public static TCast ValueForSlashOption<TCast>(this IActionSlash action, IEnumerable<SocketSlashCommandDataOption> options, string propertyName)
        {
            try
            {
                Type type = typeof(TCast);
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>).GetGenericTypeDefinition())
                    type = Nullable.GetUnderlyingType(type);

                ActionParameterSlashAttribute parameter = action.GetType().GetProperty(propertyName)?.GetCustomAttributes(false).OfType<ActionParameterSlashAttribute>().FirstOrDefault();

                if (parameter == null)
                    throw new MissingFieldException($"The {propertyName} property was not found or does not contain the {nameof(ActionParameterSlashAttribute)}.");

                var option = options.FirstOrDefault(t => t.Name == parameter.Name);
                object val = option?.Value;
                if (val == null)
                    return default;

                TCast ret;
                if (val is IConvertible)
                    ret = (TCast)Convert.ChangeType(val, type);
                else
                    ret = (TCast)(val ?? default);

                if (typeof(TCast) == typeof(string))
                    ret = (TCast)Convert.ChangeType(ret.ToString()?.Trim(), type);

                return ret;
            }
            catch
            {
                return default;
            }
        }
    }

    public interface IActionSlashParameterOptions : IActionSlash
    {
        Task<Dictionary<string, List<(string Name, string Value)>>> GenerateSlashParameterOptionsAsync(IServiceProvider services);
    }

    public interface IActionSlashAutocomplete : IActionSlash
    {
        Dictionary<string, Func<SocketAutocompleteInteraction, Task>> GenerateSlashAutocompleteOptions();
    }

    public interface IActionSlashModal : IBotAction
    {
        Dictionary<string, (Func<object[], Task> FillParametersAsync, Func<SocketModal, Task> ModalCompleteAsync)> GenerateSlashModalOptions();
    }
}