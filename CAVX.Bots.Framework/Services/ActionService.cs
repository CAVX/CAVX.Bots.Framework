using AsyncKeyedLock;
using CAVX.Bots.Framework.Extensions;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Modules;
using CAVX.Bots.Framework.Modules.Actions;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Processing;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Services;

public class ActionService(IServiceProvider services)
{
    private readonly DiscordSocketClient _discord = services.GetRequiredService<DiscordSocketClient>();
    private readonly AsyncKeyedLocker<ulong> _asyncKeyedLocker = services.GetRequiredService<AsyncKeyedLocker<ulong>>();

    public Task InitializeAsync()
    {
        _discord.Ready += ClientReadyAsync;
        _discord.InteractionCreated += Client_InteractionCreated;

        return Task.CompletedTask;
    }

    public List<IBotAction> GetAll()
    {
        var scope = services.CreateScope();

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

    public async Task<(int PopulatedCount, int DeletedCount)> PopulateCommandsAsync(bool refresh, bool conditionalGuildsOnly, string filterName, Func<ApplicationCommandProperties, RequestOptions, Task> commandCreateAsync)
    {
        int populatedCount = 0;
        int deletedCount = 0;

        var allActions = GetAll().Where(a => a.ConditionalGuildsOnly == conditionalGuildsOnly).ToList();

        var serverCommands = await _discord.Rest.GetGlobalApplicationCommands().ConfigureAwait(false);
        List<string> commandNames = new();

        var slashChildActionGroups = allActions.OfType<IActionSlash>().OfType<IActionSlashChild>()
            .Where(a => filterName is null || filterName == a.Parent.CommandName).GroupBy(a => a.Parent.CommandName);

        var slashActionsStandalone = allActions.OfType<IActionSlash>()
            .Where(a => a is not IActionSlashChild && (filterName is null || filterName == a.CommandName));

        foreach (var slashActionGroup in slashChildActionGroups)
        {
            var parent = slashActionGroup.Key == null ? null : slashActionGroup.First().Parent;
            if (parent == null || (!refresh && serverCommands.Any(c => c.Name == parent.CommandName))) continue; // and [c.Type == ApplicationCommandType.Slash] when the bug is fixed, so names can overlap

            var commandBuilder = GenerateCommand(parent);

            foreach (var slashActionChild in slashActionGroup)
            {
                if (slashActionChild is not IActionSlash slashAction) continue;

                var subOptionBuilder = new SlashCommandOptionBuilder
                {
                    Name = slashAction.CommandName,
                    Description = slashAction.CommandDescription,
                    IsRequired = false,
                    Type = ApplicationCommandOptionType.SubCommand
                };

                foreach (var parameterOption in await GenerateParametersForActionAsync(slashAction))
                    subOptionBuilder.AddOption(parameterOption);

                commandBuilder.AddOption(subOptionBuilder);
            }

            commandNames.Add(parent.CommandName); //and factor in command type later too
            await commandCreateAsync(commandBuilder.Build(), null);
            populatedCount++;
        }

        foreach (var slashAction in slashActionsStandalone)
        {
            if (!refresh && serverCommands.Any(c => c.Name == slashAction.CommandName)) continue; // and [c.Type == ApplicationCommandType.Slash] when the bug is fixed, so names can overlap

            var commandBuilder = GenerateCommand(slashAction);
            foreach (var parameterOption in await GenerateParametersForActionAsync(slashAction))
                commandBuilder.AddOption(parameterOption);

            commandNames.Add(slashAction.CommandName); //and factor in command type later too
            await commandCreateAsync(commandBuilder.Build(), null);
            populatedCount++;
        }

        var messageActions = allActions.OfType<IActionMessage>()
            .Where(a => filterName is null || filterName == a.CommandName);

        foreach (var messageAction in messageActions)
        {
            if (!refresh && serverCommands.Any(c => c.Name == messageAction.CommandName)) continue; // and [c.Type == ApplicationCommandType.Message] when the bug is fixed, so names can overlap

            var commandProperties = BuildMessageCommandProperties(messageAction);

            commandNames.Add(messageAction.CommandName); //and factor in command type later too
            await commandCreateAsync(commandProperties, null);
            populatedCount++;
        }

        var userActions = allActions.OfType<IActionUser>()
            .Where(a => filterName is null || filterName == a.CommandName);

        foreach (var userAction in userActions)
        {
            if (!refresh && serverCommands.Any(c => c.Name == userAction.CommandName)) continue; // and [c.Type == ApplicationCommandType.User] when the bug is fixed, so names can overlap

            var commandProperties = BuildUserCommandProperties(userAction);

            commandNames.Add(userAction.CommandName); //and factor in command type later too
            await commandCreateAsync(commandProperties, null);
            populatedCount++;
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

    private static SlashCommandBuilder GenerateCommand(IActionSlashRoot commandInfo)
    {
        var newCommand = new SlashCommandBuilder();
        newCommand.WithName(commandInfo.CommandName);
        newCommand.WithDescription(commandInfo.CommandDescription);
        if (commandInfo.RequiredAccessRule is { PermissionType: ActionPermissionType.RequirePermission })
            newCommand.WithDefaultMemberPermissions(commandInfo.RequiredAccessRule.RequiredPermission);

        return newCommand;
    }

    private async Task<List<SlashCommandOptionBuilder>> GenerateParametersForActionAsync(IBotAction action)
    {
        List<SlashCommandOptionBuilder> values = new();
        var parameters = action.GetParameters<ActionParameterSlashAttribute>().OrderBy(p => p.Attribute.Order).ToList();
        if (parameters.TryGetNonEnumeratedCount(out int paramCount) && paramCount == 0)
            return values;

        Dictionary<string, List<(string Name, string Value)>> generatedOptions = null;
        if (action is IActionSlashParameterOptions paramOptions)
        {
            using var scope = services.CreateScope();
            generatedOptions = await paramOptions.GenerateSlashParameterOptionsAsync(scope.ServiceProvider);
        }

        if (generatedOptions == null) return values;

        foreach (var p in parameters)
        {
            var autocompleteKeys = (action as IActionSlashAutocomplete)?.GenerateSlashAutocompleteOptions()?.Keys.ToList();
            values.Add(BuildOptionFromParameter(p.Property, p.Attribute, generatedOptions, autocompleteKeys));
        }

        return values;
    }

    private static MessageCommandProperties BuildMessageCommandProperties(IActionMessage action)
    {
        var newCommand = new MessageCommandBuilder();
        newCommand.WithName(action.CommandName);

        return newCommand.Build();
    }

    private static UserCommandProperties BuildUserCommandProperties(IActionUser action)
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
                if (guildApplicationCommands?.Any() != true) continue;

                await guildApplicationCommands.First().DeleteAsync();
                await Task.Delay(500);
            }
        });
    }

    public async Task PurgeGuildCommandsAsync(ulong guildId)
    {
        var command = (await _discord.Rest.GetGuildApplicationCommands(guildId)).FirstOrDefault();
        if (command != null)
            await command.DeleteAsync();
    }

    public async Task DeleteGuildCommandByNameAsync(ulong guildId, string name)
    {
        var command = (await _discord.Rest.GetGuildApplicationCommands(guildId)).FirstOrDefault(c => c.Name == name);
        if (command != null)
            await command.DeleteAsync();
    }

    private static SlashCommandOptionBuilder BuildOptionFromParameter(PropertyInfo property, ActionParameterSlashAttribute attribute, Dictionary<string, List<(string Name, string Value)>> generatedOptions, List<string> autocompleteParameters)
    {
        var optionBuilder = new SlashCommandOptionBuilder
        {
            Name = attribute.Name,
            Description = attribute.Description,
            IsRequired = attribute.Required,
            Type = attribute.Type.ToApplicationCommandOptionType(),
            IsDefault = null,
            IsAutocomplete = autocompleteParameters?.Contains(attribute.Name) ?? false,
        };

        var stringChoices = property.GetCustomAttributes(false).OfType<ActionParameterOptionStringAttribute>().OrderBy(c => c.Order).ToList();
        if (stringChoices.Any())
        {
            foreach (var c in stringChoices)
                optionBuilder.AddChoice(c.Name, c.Value);
        }

        var intChoices = property.GetCustomAttributes(false).OfType<ActionParameterOptionIntAttribute>().OrderBy(c => c.Order).ToList();
        if (intChoices.Any())
        {
            foreach (var c in intChoices)
                optionBuilder.AddChoice(c.Name, c.Value);
        }

        if (generatedOptions?.Any() == true)
        {
            var generatedChoices = generatedOptions.Where(o => o.Key == attribute.Name).Select(o => o.Value).FirstOrDefault();
            if (generatedChoices?.Any() == true)
            {
                foreach (var (name, value) in generatedChoices)
                    optionBuilder.AddChoice(name, value);
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
        if (collector?.Execute == null)
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
            var actionRunFactory = ActionRunFactory.Find(services, this, _asyncKeyedLocker, context, arg);
            if (actionRunFactory != null)
                _ = actionRunFactory.RunActionAsync();
        });

        return Task.CompletedTask;
    }
}