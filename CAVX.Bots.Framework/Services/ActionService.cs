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

namespace CAVX.Bots.Framework.Services
{
    public class ActionService
    {
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;

        public ActionService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
        }

        public Task InitializeAsync()
        {
            _discord.Ready += ClientReadyAsync;
            _discord.InteractionCreated += Client_InteractionCreated;
            //_discord.JoinedGuild += ClientJoinedGuildAsync;

            return Task.CompletedTask;
        }

        public List<BotAction> GetAll()
        {
            var scope = _services.CreateScope();

            var allActions = new List<BotAction>();

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => a.GetTypes())
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(BotAction)));

            foreach (Type type in types)
                allActions.Add((BotAction)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type));

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

        public async Task<int> PopulateGlobalCommandsAsync(bool refresh = false, string filterName = null)
        {
            int populatedCount = 0;

            var allActions = GetAll().OfType<BotCommandAction>();

            var globalCommands = await _discord.Rest.GetGlobalApplicationCommands().ConfigureAwait(false);
            var slashActions = allActions
                .Where(a => a.SlashCommandProperties != null && a.SlashCommandProperties is ActionGlobalSlashCommandProperties && (filterName is null || filterName == a.SlashCommandProperties.Name));

            foreach (var slashAction in slashActions)
            {
                if (refresh || !globalCommands.Any(c => c.Name == slashAction.SlashCommandProperties.Name)) // and [c.Type == ApplicationCommandType.Slash] when the bug is fixed, so names can overlap
                {
                    var commandProperties = await BuildSlashCommandPropertiesAsync(slashAction);
                    await _discord.Rest.CreateGlobalCommand(commandProperties);
                    populatedCount++;
                }
            }

            var messageActions = allActions
                .Where(a => a.MessageCommandProperties != null && a.MessageCommandProperties is ActionGlobalMessageCommandProperties && (filterName is null || filterName == a.MessageCommandProperties.Name));

            foreach (var messageAction in messageActions)
            {
                if (refresh || !globalCommands.Any(c => c.Name == messageAction.MessageCommandProperties.Name)) // and [c.Type == ApplicationCommandType.Message] when the bug is fixed, so names can overlap
                {
                    var commandProperties = BuildMessageCommandProperties(messageAction);
                    await _discord.Rest.CreateGlobalCommand(commandProperties);
                    populatedCount++;
                }
            }

            var userActions = allActions
                .Where(a => a.UserCommandProperties != null && a.UserCommandProperties is ActionGlobalUserCommandProperties && (filterName is null || filterName == a.UserCommandProperties.Name));

            foreach (var userAction in userActions)
            {
                if (refresh || !globalCommands.Any(c => c.Name == userAction.UserCommandProperties.Name)) // and [c.Type == ApplicationCommandType.User] when the bug is fixed, so names can overlap
                {
                    var commandProperties = BuildUserCommandProperties(userAction);
                    await _discord.Rest.CreateGlobalCommand(commandProperties);
                    populatedCount++;
                }
            }

            /* See below
            var guildIds = (await _discord.Rest.GetGuildsAsync()).Select(g => g.Id);
            foreach (var guildId in guildIds)
            {
                var guild = _discord.GetGuild(guildId);
                if (guild != null)
                    await SetOwnerPermissionsAsync(guild);
            }
            */

            return populatedCount;
        }

        public async Task AddGuildCommandAsync(ulong guildId, string name)
        {
            var allActions = GetAll().OfType<BotCommandAction>();

            var slashActions = allActions.Where(a => a.SlashCommandProperties != null && a.SlashCommandProperties is ActionGuildSlashCommandProperties && a.SlashCommandProperties.Name == name);

            foreach (var slashAction in slashActions)
            {
                var commandProperties = await BuildSlashCommandPropertiesAsync(slashAction);
                await _discord.Rest.CreateGuildCommand(commandProperties, guildId);
            }

            var messageActions = allActions.Where(a => a.MessageCommandProperties != null && a.MessageCommandProperties is ActionGuildMessageCommandProperties && a.MessageCommandProperties.Name == name);

            foreach (var messageAction in messageActions)
            {
                var commandProperties = BuildMessageCommandProperties(messageAction);
                await _discord.Rest.CreateGuildCommand(commandProperties, guildId);
            }

            var userActions = allActions.Where(a => a.UserCommandProperties != null && a.UserCommandProperties is ActionGuildUserCommandProperties && a.UserCommandProperties.Name == name);

            foreach (var userAction in userActions)
            {
                var commandProperties = BuildUserCommandProperties(userAction);
                await _discord.Rest.CreateGuildCommand(commandProperties, guildId);
            }
        }

        private async Task<SlashCommandProperties> BuildSlashCommandPropertiesAsync(BotCommandAction action)
        {
            var newCommand = new SlashCommandBuilder();
            newCommand.WithName(action.SlashCommandProperties.Name);
            newCommand.WithDescription(action.SlashCommandProperties.Description);

            var parameters = action.GetParameters<ActionParameterSlashAttribute>()?.OrderBy(p => p.Attribute.Order);
            if (parameters != null)
            {
                var filteredParameters = parameters.Where(p => p.Attribute.ParentNames == null || !p.Attribute.ParentNames.Any()).ToList();
                if (filteredParameters != null && filteredParameters.Any())
                {
                    Dictionary<string, List<(string Name, string Value)>> generatedOptions = null;
                    if (action.SlashCommandProperties.GenerateParameterOptionsAsync != null)
                    {
                        using var scope = _services.CreateScope();
                        generatedOptions = await action.SlashCommandProperties.GenerateParameterOptionsAsync(scope.ServiceProvider);
                    }

                    foreach (var p in filteredParameters)
                    {
                        var option = BuildOptionFromParameter(p.Property, p.Attribute, parameters.ToList(), generatedOptions, action.SlashCommandProperties.AutocompleteAsync?.Keys.ToList());

                        newCommand.AddOption(option);
                    }
                }
            }
            //newCommand.WithDefaultPermission(action.RequiredPermissions == null);

            return newCommand.Build();
        }

        private MessageCommandProperties BuildMessageCommandProperties(BotCommandAction action)
        {
            var newCommand = new MessageCommandBuilder();
            newCommand.WithName(action.MessageCommandProperties.Name);

            return newCommand.Build();
        }

        private UserCommandProperties BuildUserCommandProperties(BotCommandAction action)
        {
            var newCommand = new UserCommandBuilder();
            newCommand.WithName(action.UserCommandProperties.Name);

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
            var option = new SlashCommandOptionBuilder()
            {
                Name = attribute.Name,
                Description = attribute.Description,
                IsRequired = attribute.Required,
                Type = attribute.Type,
                IsDefault = attribute.DefaultSubCommand ? true : null,
                IsAutocomplete = autocompleteParameters?.Contains(attribute.Name) ?? false,
            };

            var stringChoices = property.GetCustomAttributes(false).OfType<ActionParameterOptionStringAttribute>()?.OrderBy(c => c.Order);
            if (stringChoices != null && stringChoices.Any())
            {
                foreach (var c in stringChoices)
                    option.AddChoice(c.Name, c.Value);
            }

            var intChoices = property.GetCustomAttributes(false).OfType<ActionParameterOptionIntAttribute>()?.OrderBy(c => c.Order);
            if (intChoices != null && intChoices.Any())
            {
                foreach (var c in intChoices)
                    option.AddChoice(c.Name, c.Value);
            }

            if (generatedOptions != null && generatedOptions.Any())
            {
                var generatedChoices = generatedOptions.Where(o => o.Key == attribute.Name).Select(o => o.Value).FirstOrDefault();
                if (generatedChoices != null && generatedChoices.Any())
                {
                    foreach (var c in generatedChoices)
                        option.AddChoice(c.Name, c.Value);
                }
            }

            var filteredParameters = parameters.Where(p => p.Attribute.ParentNames != null && p.Attribute.ParentNames.Contains(attribute.Name)).ToList();
            if (filteredParameters != null && filteredParameters.Any())
            {
                foreach (var p in filteredParameters)
                {
                    var subOption = BuildOptionFromParameter(p.Property, p.Attribute, filteredParameters, null, autocompleteParameters);

                    option.AddOption(subOption);
                }
            }

            return option;
        }

        private readonly ConcurrentDictionary<ulong, ICollectorLogic> _inProgressCollectors = new();

        internal void RegisterCollector(ICollectorLogic collector) => _inProgressCollectors.GetOrAdd(collector.MessageId, collector);
        internal void UnregisterCollector(ICollectorLogic collector) => _inProgressCollectors.Remove(collector.MessageId, out _);

        public bool CollectorAvailable(ulong messageId)
        {
            return _inProgressCollectors.TryGetValue(messageId, out _);
        }

        public async Task<(bool Success, string FailureMessage, IMessageBuilder MessageBuilder)> FireCollectorAsync(IUser user, ulong messageId, object[] idParams, object[] selectParams)
        {
            _inProgressCollectors.TryGetValue(messageId, out ICollectorLogic collector);
            if (collector == null || collector.Execute == null)
                return (false, "I couldn't find that action anymore. Maybe you were too late?", null);
            if (collector.OnlyOriginalUserAllowed && (!collector.OriginalUserId.HasValue || collector.OriginalUserId != user.Id))
                return (false, "Sorry, for this outcome, only the original user gets to pick!", null);

            return await collector.Execute(user, messageId, idParams, selectParams);
        }

        private async Task Client_InteractionCreated(SocketInteraction arg)
        {
            var context = new RequestInteractionContext(arg, _discord);
            var actionRunFactory = ActionRunFactory.Find(this, context, arg);
            if (actionRunFactory != null)
                await actionRunFactory.RunActionAsync();
        }
    }
}