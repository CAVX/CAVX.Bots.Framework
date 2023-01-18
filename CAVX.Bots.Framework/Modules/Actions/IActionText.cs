using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Discord.Commands.Builders;
using Discord.WebSocket;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionText : IBotAction
    {
        string CommandName { get; }
        List<string> CommandAliases { get; }
        string CommandHelpSummary { get; }
        int? TextParserPriority { get; }

        Task FillTextParametersAsync(object[] options);
    }

    public interface IActionTextModifyBuilder : IActionText
    {
        Task ModifyBuilderAsync(IServiceProvider services, CommandBuilder builder);
    }
}