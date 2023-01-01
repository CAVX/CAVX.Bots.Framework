using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Services;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Authentication.ExtendedProtection;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using CAVX.Bots.Framework.Processing;
using CAVX.Bots.Framework.Modules.Attributes;
using Discord.Rest;
using Discord.Commands.Builders;
using CAVX.Bots.Framework.Modules.Actions;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Modules.Actions.Attributes;

namespace CAVX.Bots.Framework.Modules
{
    [ModuleOrder(2)]
    public class GeneratedActionCommands : MessageCommandBase
    {
        // DI Services
        public ActionService ActionService { get; set; }
        public IServiceProvider ServiceProvider { get; set; }

        protected override void OnModuleBuilding(CommandService commandService, ModuleBuilder builder)
        {
            base.OnModuleBuilding(commandService, builder);

            using var scope = ServiceProvider.CreateScope();

            //Get phrases from items as aliases for the referenced command.
            foreach (var action in ActionService.GetAll().OfType<IActionText>())
            {
                builder.AddCommand(action.CommandName, RunActionFromTextCommand,
                    async builder =>
                    {
                        if (action.CommandAliases != null)
                            builder.AddAliases(action.CommandAliases.ToArray());
                        builder.Summary = action.CommandHelpSummary;
                        if (action.RestrictAccessToGuilds)
                            builder.AddPrecondition(new RequireContextAttribute(ContextType.Guild));
                        if (action.RequiredAccessRule != null)
                        {
                            if (action.RequiredAccessRule.PermissionType == Models.ActionPermissionType.RequireOwner)
                                builder.AddPrecondition(new RequireOwnerAttribute());
                            else if (action.RequiredAccessRule.PermissionType == Models.ActionPermissionType.RequirePermission && action.RequiredAccessRule.RequiredPermission.HasValue)
                                builder.AddPrecondition(new RequireUserPermissionAttribute(action.RequiredAccessRule.RequiredPermission.Value));
                        }
                        if (action.TextParserPriority.HasValue)
                            builder.Priority = action.TextParserPriority.Value;

                        var parameters = action.GetParameters<ActionParameterTextAttribute>()?.OrderBy(p => p.Attribute.Order);
                        if (parameters != null)
                        {
                            foreach (var p in parameters)
                            {
                                builder.AddParameter(p.Attribute.Name, p.Attribute.ParameterType, pb =>
                                {
                                    pb.Summary = p.Attribute.Description;
                                    pb.IsMultiple = p.Attribute.IsMultiple;
                                    pb.IsRemainder = p.Attribute.IsRemainder;
                                    pb.DefaultValue = p.Attribute.DefaultValue;
                                    pb.IsOptional = !p.Attribute.Required;
                                });
                            }
                        }

                        if (action is IActionTextModifyBuilder mbAction)
                            await mbAction.ModifyBuilderAsync(scope.ServiceProvider, builder);
                    }
                );
            }
        }

        public async Task RunActionFromTextCommand(ICommandContext commandContext, object[] parmValues, IServiceProvider services, CommandInfo commandInfo)
        {
            var actionService = services.GetRequiredService<ActionService>();

            var context = new RequestCommandContext(commandContext as SocketCommandContext);
            var actionRunFactory = ActionRunFactory.Find(services, actionService, context, commandInfo, parmValues);
            if (actionRunFactory != null)
                await actionRunFactory.RunActionAsync();
        }
    }
}