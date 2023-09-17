using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Modules.Actions;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Processing;
using CAVX.Bots.Framework.Models;
using System.Collections.Concurrent;
using CAVX.Bots.Framework.Modules;
using CAVX.Bots.Framework.Extensions;
using AsyncKeyedLock;

namespace CAVX.Bots.Framework.Services
{
    public class ActionService
    {
        private readonly DiscordSocketClient _discord;
        private readonly AsyncKeyedLocker<ulong> _asyncKeyedLocker;
        private readonly IServiceProvider _services;

        public ActionService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _asyncKeyedLocker = services.GetRequiredService<AsyncKeyedLocker<ulong>>();
            _services = services;
        }

        public Task InitializeAsync()
        {
            _discord.Ready += ClientReadyAsync;
            _discord.InteractionCreated += Client_InteractionCreated;
            //_discord.JoinedGuild += ClientJoinedGuildAsync;

            return Task.CompletedTask;
        }

        public List<IBotAction> GetAll()
        {
            var scope = _services.CreateScope();

            var allActions = new List<IBotAction>();

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => a.GetTypes())
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsAssignableTo(typeof(IBotAction)));

            foreach (Type type in types)
                allActions.Add((IBotAction)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type));

            return allActions;
        }

        private async Task ClientReadyAsync()
        {
            _discord.Ready -= ClientReadyAsync;

            try
            {
                await PopulateGlobalCommandsAsync();
                PurgeAllGuildCommands();
            }
            catch (HttpException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
        }

        public async Task<(int PopulatedCount, int DeletedCount)> PopulateGlobalCommandsAsync(bool refresh = false, string filterName = null) => await PopulateCommandsAsync(refresh, false, filterName, _discord.Rest.CreateGlobalCommand);

        public async Task<(int PopulatedCount, int DeletedCount)> AddGuildCommandAsync(ulong guildId, string name) => await PopulateCommandsAsync(true, true, name, (props, options) => _discord.Rest.CreateGuildCommand(props, guildId, options));

        public async Task<(int PopulatedCount, int DeletedCount)> PopulateCommandsAsync(bool refresh, bool conditionalGuildsOnly, string filterName, Func<ApplicationCommandProperties, RequestOptions, Task> CommandCreateAsync)
        {
            int populatedCount = 0;
            int deletedCount = 0;

            var allActions = GetAll().OfType<IBotAction>().Where(a => a.ConditionalGuildsOnly == conditionalGuildsOnly);

            var serverCommands = await _discord.Rest.GetGlobalApplicationCommands().ConfigureAwait(false);
            List<string> commandNames = new();

            var slashChildActionGroups = allActions.OfType<IActionSlash>().OfType<IActionSlashChild>()
                .Where(a => filterName is null || filterName == a.Parent.CommandName).GroupBy(a => a.Parent.CommandName);

            var slashActionsStandalone = allActions.OfType<IActionSlash>()
                .Where(a => a is not IActionSlashChild && (filterName is null || filterName == a.CommandName));

            foreach (var slashActionGroup in slashChildActionGroups)
            {
                var parent = slashActionGroup.Key == null ? null : slashActionGroup.First().Parent;
                if (refresh || !serverCommands.Any(c => c.Name == parent.CommandName)) // and [c.Type == ApplicationCommandType.Slash] when the bug is fixed, so names can overlap
                {
                    var commandBuilder = GenerateCommand(parent);

                    foreach (var slashActionChild in slashActionGroup)
                    {
                        var slashAction = slashActionChild as IActionSlash;
                        var subOptionBuilder = new SlashCommandOptionBuilder()
                        {
                            Name = slashAction.CommandName,
                            Description = slashAction.CommandDescription,
                            IsRequired = false,
                            Type = ApplicationCommandOptionType.SubCommand
                        };

                        foreach(var parameterOption in await GenerateParametersForActionAsync(slashAction))
                            subOptionBuilder.AddOption(parameterOption);

                        commandBuilder.AddOption(subOptionBuilder);
                    }

                    commandNames.Add(parent.CommandName); //and factor in command type later too
                    await CommandCreateAsync(commandBuilder.Build(), null);
                    populatedCount++;
                }
            }

            foreach (var slashAction in slashActionsStandalone)
            {
                if (refresh || !serverCommands.Any(c => c.Name == slashAction.CommandName)) // and [c.Type == ApplicationCommandType.Slash] when the bug is fixed, so names can overlap
                {
                    var commandBuilder = GenerateCommand(slashAction);
                    foreach (var parameterOption in await GenerateParametersForActionAsync(slashAction))
                        commandBuilder.AddOption(parameterOption);

                    commandNames.Add(slashAction.CommandName); //and factor in command type later too
                    await CommandCreateAsync(commandBuilder.Build(), null);
                    populatedCount++;
                }
            }

            var messageActions = allActions.OfType<IActionMessage>()
                .Where(a => filterName is null || filterName == a.CommandName);

            foreach (var messageAction in messageActions)
            {
                if (refresh || !serverCommands.Any(c => c.Name == messageAction.CommandName)) // and [c.Type == ApplicationCommandType.Message] when the bug is fixed, so names can overlap
                {
                    var commandProperties = BuildMessageCommandProperties(messageAction);

                    commandNames.Add(messageAction.CommandName); //and factor in command type later too
                    await CommandCreateAsync(commandProperties, null);
                    populatedCount++;
                }
            }

            var userActions = allActions.OfType<IActionUser>()
                .Where(a => filterName is null || filterName == a.CommandName);

            foreach (var userAction in userActions)
            {
                if (refresh || !serverCommands.Any(c => c.Name == userAction.CommandName)) // and [c.Type == ApplicationCommandType.User] when the bug is fixed, so names can overlap
                {
                    var commandProperties = BuildUserCommandProperties(userAction);

                    commandNames.Add(userAction.CommandName); //and factor in command type later too
                    await CommandCreateAsync(commandProperties, null);
                    populatedCount++;
                }
            }

            if (refresh && filterName is null && populatedCount > 0 && serverCommands.ExistsWithItems())
            {
                foreach (var command in serverCommands.Where(c => !commandNames.Contains(c.Name)))
                {
                    await command.DeleteAsync();
                    deletedCount++;
                }
            }

            return (populatedCount, deletedCount);
        }

        private SlashCommandBuilder GenerateCommand(IActionSlashRoot commandInfo)
        {
            var newCommand = new SlashCommandBuilder();
            newCommand.WithName(commandInfo.CommandName);
            newCommand.WithDescription(commandInfo.CommandDescription);
            if (commandInfo.RequiredAccessRule != null && commandInfo.RequiredAccessRule.PermissionType == ActionPermissionType.RequirePermission)
                newCommand.WithDefaultMemberPermissions(commandInfo.RequiredAccessRule.RequiredPermission);

            return newCommand;
        }

        private async Task<List<SlashCommandOptionBuilder>> GenerateParametersForActionAsync(IActionSlash action)
        {
            List<SlashCommandOptionBuilder> values = new();
            var parameters = action.GetParameters<ActionParameterSlashAttribute>()?.OrderBy(p => p.Attribute.Order);
            if (parameters.ExistsWithItems())
            {
                Dictionary<string, List<(string Name, string Value)>> generatedOptions = null;
                if (action is IActionSlashParameterOptions paramOptions)
                {
                    using var scope = _services.CreateScope();
                    generatedOptions = await paramOptions.GenerateSlashParameterOptionsAsync(scope.ServiceProvider);
                }

                foreach (var p in parameters)
                {
                    var autocompleteKeys = (action as IActionSlashAutocomplete)?.GenerateSlashAutocompleteOptions()?.Keys.ToList();
                    values.Add(BuildOptionFromParameter(p.Property, p.Attribute, parameters.ToList(), generatedOptions, autocompleteKeys));
                }
            }
            return values;
        }

        private MessageCommandProperties BuildMessageCommandProperties(IActionMessage action)
        {
            var newCommand = new MessageCommandBuilder();
            newCommand.WithName(action.CommandName);

            return newCommand.Build();
        }

        private UserCommandProperties BuildUserCommandProperties(IActionUser action)
        {
            var newCommand = new UserCommandBuilder();
            newCommand.WithName(action.CommandName);

            return newCommand.Build();
        }

        private void PurgeAllGuildCommands()
        {
            _ = Task.Run(async () =>
            {
                foreach (var guildSummary in await _discord.Rest.GetGuildSummariesAsync().FlattenAsync())
                {
                    var guildApplicationCommands = await _discord.Rest.GetGuildApplicationCommands(guildSummary.Id);
                    if (guildApplicationCommands != null && guildApplicationCommands.Any())
                    {
                        await guildApplicationCommands.First().DeleteAsync();
                        await Task.Delay(500);
                    }
                }
            });
        }

        public async Task PurgeGuildCommandsAsync(ulong guildId)
        {
            await (await _discord.Rest.GetGuildApplicationCommands(guildId)).FirstOrDefault()?.DeleteAsync();
        }

        public async Task DeleteGuildCommandByNameAsync(ulong guildId, string name)
        {
            await (await _discord.Rest.GetGuildApplicationCommands(guildId)).FirstOrDefault(c => c.Name == name)?.DeleteAsync();
        }

        private SlashCommandOptionBuilder BuildOptionFromParameter(PropertyInfo property, ActionParameterSlashAttribute attribute, List<(PropertyInfo Property, ActionParameterSlashAttribute Attribute)> parameters, Dictionary<string, List<(string Name, string Value)>> generatedOptions, List<string> autocompleteParameters)
        {
            var optionBuilder = new SlashCommandOptionBuilder()
            {
                Name = attribute.Name,
                Description = attribute.Description,
                IsRequired = attribute.Required,
                Type = attribute.Type.ToApplicationCommandOptionType(),
                IsDefault = null,
                IsAutocomplete = autocompleteParameters?.Contains(attribute.Name) ?? false,
            };

            var stringChoices = property.GetCustomAttributes(false).OfType<ActionParameterOptionStringAttribute>()?.OrderBy(c => c.Order);
            if (stringChoices != null && stringChoices.Any())
            {
                foreach (var c in stringChoices)
                    optionBuilder.AddChoice(c.Name, c.Value);
            }

            var intChoices = property.GetCustomAttributes(false).OfType<ActionParameterOptionIntAttribute>()?.OrderBy(c => c.Order);
            if (intChoices != null && intChoices.Any())
            {
                foreach (var c in intChoices)
                    optionBuilder.AddChoice(c.Name, c.Value);
            }

            if (generatedOptions != null && generatedOptions.Any())
            {
                var generatedChoices = generatedOptions.Where(o => o.Key == attribute.Name).Select(o => o.Value).FirstOrDefault();
                if (generatedChoices != null && generatedChoices.Any())
                {
                    foreach (var c in generatedChoices)
                        optionBuilder.AddChoice(c.Name, c.Value);
                }
            }

            return optionBuilder;
        }

        private readonly ConcurrentDictionary<ulong, ICollectorLogic> _inProgressCollectors = new();

        internal void RegisterCollector(ICollectorLogic collector) => _inProgressCollectors.GetOrAdd(collector.MessageId, collector);
        internal void UnregisterCollector(ICollectorLogic collector) => _inProgressCollectors.Remove(collector.MessageId, out _);

        public bool CollectorAvailable(ulong messageId)
        {
            return _inProgressCollectors.TryGetValue(messageId, out _);
        }

        public async Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)> FireCollectorAsync(IUser user, ulong messageId, object[] idParams, object[] selectParams)
        {
            _inProgressCollectors.TryGetValue(messageId, out ICollectorLogic collector);
            if (collector == null || collector.Execute == null)
                return (MessageResultCode.PreconditionFailed, "I couldn't find that action anymore. Maybe you were too late?", null);
            if (collector.OnlyOriginalUserAllowed && (!collector.OriginalUserId.HasValue || collector.OriginalUserId != user.Id))
                return (MessageResultCode.PreconditionFailed, "Sorry, for this outcome, only the original user gets to pick!", null);

            return await collector.Execute(user, messageId, idParams, selectParams);
        }

        private Task Client_InteractionCreated(SocketInteraction arg)
        {
            _ = Task.Run(() =>
            {
                var context = new RequestInteractionContext(arg, _discord);
                var actionRunFactory = ActionRunFactory.Find(_services, this, _asyncKeyedLocker, context, arg);
                if (actionRunFactory != null)
                    _ = actionRunFactory.RunActionAsync();
            });

            return Task.CompletedTask;
        }
    }
}