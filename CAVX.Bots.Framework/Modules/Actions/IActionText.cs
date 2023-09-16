using Discord.Commands.Builders;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions;

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
    void ModifyBuilder(IServiceProvider services, CommandBuilder builder);
}