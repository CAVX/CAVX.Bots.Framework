using AsyncKeyedLock;
using CAVX.Bots.Framework.Modules.Actions;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Processing;
using CAVX.Bots.Framework.Services;
using Discord.Commands;
using Discord.Commands.Builders;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules;

[ModuleOrder(2)]
public class GeneratedActionCommands : MessageCommandBase
{
    // DI Services
    public ActionService ActionService { get; set; }

    public IServiceProvider ServiceProvider { get; set; }

    protected override void OnModuleBuilding(CommandService commandService, ModuleBuilder builder)
    {
        base.OnModuleBuilding(commandService, builder);

        //Get phrases from items as aliases for the referenced command.
        foreach (var action in ActionService.GetAll().OfType<IActionText>())
        {
            builder.AddCommand(action.CommandName, RunActionFromTextCommand,
                commandBuilder =>
                {
                    using var scope = ServiceProvider.CreateScope();

                    if (action.CommandAliases != null)
                        commandBuilder.AddAliases(action.CommandAliases.ToArray());
                    commandBuilder.Summary = action.CommandHelpSummary;
                    if (action.RestrictAccessToGuilds)
                        commandBuilder.AddPrecondition(new RequireContextAttribute(ContextType.Guild));
                    if (action.RequiredAccessRule != null)
                    {
                        if (action.RequiredAccessRule.PermissionType == Models.ActionPermissionType.RequireOwner)
                            commandBuilder.AddPrecondition(new RequireOwnerAttribute());
                        else if (action.RequiredAccessRule.PermissionType == Models.ActionPermissionType.RequirePermission && action.RequiredAccessRule.RequiredPermission.HasValue)
                            commandBuilder.AddPrecondition(new RequireUserPermissionAttribute(action.RequiredAccessRule.RequiredPermission.Value));
                    }
                    if (action.TextParserPriority.HasValue)
                        commandBuilder.Priority = action.TextParserPriority.Value;

                    var parameters = action.GetParameters<ActionParameterTextAttribute>().OrderBy(p => p.Attribute.Order);
                    foreach (var attribute in parameters.Select(p => p.Attribute))
                    {
                        commandBuilder.AddParameter(attribute.Name, attribute.ParameterType, pb =>
                        {
                            pb.Summary = attribute.Description;
                            pb.IsMultiple = attribute.IsMultiple;
                            pb.IsRemainder = attribute.IsRemainder;
                            pb.DefaultValue = attribute.DefaultValue;
                            pb.IsOptional = !attribute.Required;
                        });
                    }

                    if (action is IActionTextModifyBuilder mbAction)
                        mbAction.ModifyBuilder(scope.ServiceProvider, commandBuilder);
                }
            );
        }
    }

    public static Task RunActionFromTextCommand(ICommandContext commandContext, object[] parmValues, IServiceProvider services, CommandInfo commandInfo)
    {
        var actionService = services.GetRequiredService<ActionService>();
        var asyncKeyedLocker = services.GetRequiredService<AsyncKeyedLocker<ulong>>();

        var context = new RequestCommandContext(commandContext as SocketCommandContext);
        var actionRunFactory = ActionRunFactory.Find(services, actionService, asyncKeyedLocker, context, commandInfo, parmValues);
        if (actionRunFactory != null)
            _ = actionRunFactory.RunActionAsync();

        return Task.CompletedTask;
    }
}