using AsyncKeyedLock;
using CAVX.Bots.Framework.Extensions;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Modules.Actions;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Services;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Utilities;
using Discord;

namespace CAVX.Bots.Framework.Processing;

public abstract class ActionRunFactory
{
    public abstract Task RunActionAsync();

    public static ActionRunFactory Find(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketInteraction interaction)
    {
        return interaction switch
        {
            SocketSlashCommand slashCommand => new ActionSlashRunFactory(services, actionService, asyncKeyedLocker, context, slashCommand),
            SocketMessageCommand msgCommand => new ActionMessageRunFactory(services, actionService, asyncKeyedLocker, context, msgCommand),
            SocketUserCommand userCommand => new ActionUserRunFactory(services, actionService, asyncKeyedLocker, context, userCommand),
            SocketAutocompleteInteraction autocompleteInteraction => new ActionAutocompleteResponseFactory(services, actionService, asyncKeyedLocker, context, autocompleteInteraction),
            SocketModal modalInteraction => new ActionModalResponseFactory(services, actionService, asyncKeyedLocker, context, modalInteraction),
            SocketMessageComponent component => new ActionComponentRunFactory(services, actionService, asyncKeyedLocker, context, component),
            _ => null
        };
    }

    public static ActionRunFactory Find(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, CommandInfo commandInfo, object[] parmValues) => new ActionTextRunFactory(services, actionService, asyncKeyedLocker, context, commandInfo, parmValues);
}

public abstract class ActionRunFactory<TInteraction, TAction>(IServiceProvider services,
        ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context,
        TInteraction interaction)
    : ActionRunFactory
    where TInteraction : class
    where TAction : IBotAction
{
    protected IServiceProvider _services = services;
    protected TInteraction _interaction = interaction;
    protected RequestContext _context = context;
    protected ActionService _actionService = actionService;
    protected AsyncKeyedLocker<ulong> _asyncKeyedLocker = asyncKeyedLocker;

    protected abstract string InteractionNameForLog { get; }
    protected virtual ActionRunContext RunContext => ActionRunContext.None;

    protected abstract TAction GetAction();

    protected abstract Task PopulateParametersAsync(TAction action);

    protected virtual bool ValidateParameters => true;

    protected virtual async Task RunActionAsync(TAction action)
    {
        await action.RunAsync(RunContext);
    }

    public override async Task RunActionAsync()
    {
        CancellationTokenSource ts = null;
        if (_context is RequestCommandContext)
        {
            ts = new();
            CancellationToken ct = ts.Token;
            _ = Task.Run(async () =>
            {
                using IDisposable typingObject = _context is RequestCommandContext ? _context.Channel?.EnterTypingState() : null;
                await Task.Delay(5000, ct);
            }, ct);
        }

        var action = GetAction() ?? throw new CommandInvalidException();
        action.Initialize(_context);

        if (_interaction is SocketInteraction si && _context is RequestInteractionContext ic && !action.SkipDefer)
            QueueDefer(action, si, ic);

        if (action.UseQueue && _context.Guild != null)
        {
            using (await _asyncKeyedLocker.LockAsync(_context.Guild.Id))
            {
                action = GetAction(); //refresh scope
                action.Initialize(_context);
                await ExecuteActionAsync(action, ts);
            }
        }
        else
        {
            _ = ExecuteActionAsync(action, ts);
        }
    }

    private async Task ExecuteActionAsync(TAction action, CancellationTokenSource ts)
    {
        if (!await PopulateAndValidateParametersAsync(action))
            return;
        if (!await CheckPreconditionsAsync(action))
            return;

        await RunAndHandleActionAsync(action);
        ts?.Cancel();
    }

    private static void QueueDefer(TAction action, IDiscordInteraction di, RequestInteractionContext ic)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                double secondsToWait = 2.1d - (DateTime.UtcNow - di.CreatedAt.UtcDateTime).TotalSeconds;
                if (secondsToWait > 2.1)
                    secondsToWait = 2.1;

                if (secondsToWait > 0)
                    await Task.Delay((secondsToWait * 1000).IntLop(Math.Floor));

                await ic.HadBeenAcknowledgedAsync(RequestAcknowledgeStatus.Acknowledged, async () => await di.DeferAsync(action.EphemeralRule.ToEphemeral()));
            }
            catch
            {
                // ignored
            }
        });
    }

    private async Task<bool> PopulateAndValidateParametersAsync(TAction action)
    {
        try
        {
            await PopulateParametersAsync(action);
            if (ValidateParameters && !action.ValidateParameters<ActionParameterSlashAttribute>())
            {
                await _context.ReplyAsync(EphemeralRule.EphemeralOrFallback, "Something went wrong - you didn't fill in a required option!").ConfigureAwait(false);
                return false;
            }
        }
        catch (CommandParameterValidationException ce)
        {
            await _context.ReplyAsync(EphemeralRule.EphemeralOrFallback, ce.Message);
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await _context.ReplyAsync(EphemeralRule.EphemeralOrFallback, "I couldn't understand something you entered in!").ConfigureAwait(false);
            await Logger.Instance.LogErrorAsync(_context, InteractionNameForLog, e);
            return false;
        }
        return true;
    }

    protected virtual async Task<bool> CheckPreconditionsAsync(TAction action)
    {
        var (success, message) = await action.CheckPreconditionsAsync(RunContext);
        if (success) return true;

        await _context.ReplyAsync(EphemeralRule.EphemeralOrFallback, message ?? "Something went wrong with using this command!").ConfigureAwait(false);
        return false;
    }

    private async Task RunAndHandleActionAsync(TAction action)
    {
        try
        {
            await RunActionAsync(action);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await _context.ReplyAsync(action.EphemeralRule, "Something went wrong!").ConfigureAwait(false);
            await Logger.Instance.LogErrorAsync(_context, InteractionNameForLog, e);
        }
    }
}

public class ActionSlashRunFactory(IServiceProvider services, ActionService actionService,
        AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketSlashCommand interaction)
    : ActionRunFactory<SocketSlashCommand, IActionSlash>(services, actionService, asyncKeyedLocker, context, interaction)
{
    private IEnumerable<SocketSlashCommandDataOption> _subOptions;

    protected override string InteractionNameForLog => _interaction.Data.Name;
    protected override ActionRunContext RunContext => ActionRunContext.Slash;

    protected override IActionSlash GetAction()
    {
        var (subCommandName, subOptions) = _interaction.Data.Options.GetSelectedSubOption();

        if (subCommandName != null)
        {
            _subOptions = subOptions;
            return _actionService.GetAll().OfType<IActionSlashChild>().Where(a => a.Parent.CommandName == _interaction.Data.Name).OfType<IActionSlash>().FirstOrDefault(a => a.CommandName == subCommandName);
        }

        return _actionService.GetAll().OfType<IActionSlash>().FirstOrDefault(a => a.CommandName == _interaction.Data.Name);
    }

    protected override async Task PopulateParametersAsync(IActionSlash action)
    {
        await action.FillSlashParametersAsync((_subOptions ?? _interaction.Data.Options).ToArray());
    }
}

public class ActionMessageRunFactory(IServiceProvider services, ActionService actionService,
        AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketMessageCommand interaction)
    : ActionRunFactory<SocketMessageCommand, IActionMessage>(services, actionService, asyncKeyedLocker, context, interaction)
{
    protected override string InteractionNameForLog => _interaction.Data.Name;
    protected override ActionRunContext RunContext => ActionRunContext.Message;

    protected override IActionMessage GetAction() => _actionService.GetAll().OfType<IActionMessage>().FirstOrDefault(a => a.CommandName == _interaction.Data.Name);

    protected override async Task PopulateParametersAsync(IActionMessage action)
    {
        await action.FillMessageParametersAsync(_interaction.Data.Message);
    }
}

public class ActionUserRunFactory(IServiceProvider services, ActionService actionService,
        AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketUserCommand interaction)
    : ActionRunFactory<SocketUserCommand, IActionUser>(services, actionService, asyncKeyedLocker, context, interaction)
{
    protected override string InteractionNameForLog => _interaction.Data.Name;
    protected override ActionRunContext RunContext => ActionRunContext.User;

    protected override IActionUser GetAction() => _actionService.GetAll().OfType<IActionUser>().FirstOrDefault(a => a.CommandName == _interaction.Data.Name);

    protected override async Task PopulateParametersAsync(IActionUser action)
    {
        await action.FillUserParametersAsync(_interaction.Data.Member);
    }
}

public class ActionComponentRunFactory : ActionRunFactory<SocketMessageComponent, IActionComponent>
{
    private readonly string _commandTypeName;
    private readonly object[] _idOptions;

    protected override string InteractionNameForLog => _interaction.Data.CustomId;
    protected override ActionRunContext RunContext => ActionRunContext.Component;

    public ActionComponentRunFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketMessageComponent interaction) : base(services, actionService, asyncKeyedLocker, context, interaction)
    {
        if (string.IsNullOrWhiteSpace(_interaction.Data.CustomId))
            throw new CommandInvalidException();

        var splitId = _interaction.Data.CustomId.Split('.').Where(x => !string.IsNullOrEmpty(x)).ToArray();
        _commandTypeName = splitId[0];
        _idOptions = splitId.Skip(1).Cast<object>().ToArray();
    }

    protected override IActionComponent GetAction() => _actionService.GetAll().OfType<IActionComponent>().FirstOrDefault(a => a.GetType().Name == _commandTypeName);

    protected override async Task PopulateParametersAsync(IActionComponent action)
    {
        var selectOptions = _interaction.Data.Values?.Cast<object>().ToArray();
        await action.FillComponentParametersAsync(selectOptions, _idOptions);
    }
}

public class ActionAutocompleteResponseFactory(IServiceProvider services, ActionService actionService,
        AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketAutocompleteInteraction interaction)
    : ActionRunFactory<SocketAutocompleteInteraction, IActionSlashAutocomplete>(services, actionService, asyncKeyedLocker, context, interaction)
{
    protected override string InteractionNameForLog => _interaction.Data.CommandName;

    protected override IActionSlashAutocomplete GetAction() => _actionService.GetAll().OfType<IActionSlashAutocomplete>().FirstOrDefault(a => a.CommandName == _interaction.Data.CommandName);

    protected override Task<bool> CheckPreconditionsAsync(IActionSlashAutocomplete action) => Task.FromResult(true);

    protected override bool ValidateParameters => false;

    protected override Task PopulateParametersAsync(IActionSlashAutocomplete action) => Task.CompletedTask;

    protected override async Task RunActionAsync(IActionSlashAutocomplete action)
    {
        try
        {
            var autocompleteOptions = action.GenerateSlashAutocompleteOptions();
            if (autocompleteOptions.ContainsKey(_interaction.Data.Current.Name))
                await autocompleteOptions[_interaction.Data.Current.Name](_interaction);
        }
        catch (TimeoutException)
        {
            // ignored
        }
    }
}

public class ActionModalResponseFactory : ActionRunFactory<SocketModal, IActionSlashModal>
{
    private readonly string _modalCustomId;
    private readonly object[] _idOptions;

    private Dictionary<string, (Func<object[], Task> FillParametersAsync, Func<SocketModal, Task> ModalCompleteAsync)> _modalOptions;

    protected override string InteractionNameForLog => _interaction.Data.CustomId;

    public ActionModalResponseFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketModal interaction) : base(services, actionService, asyncKeyedLocker, context, interaction)
    {
        var splitId = _interaction.Data.CustomId.Split('.');
        _modalCustomId = splitId[0];
        _idOptions = splitId.Skip(1).Cast<object>().ToArray();
    }

    protected override IActionSlashModal GetAction()
    {
        var selectedModal = _actionService.GetAll().OfType<IActionSlashModal>().Select(a =>
            {
                var modalOptions = a.GenerateSlashModalOptions();
                return new { Action = a, ModalOptions = modalOptions };
            })
            .FirstOrDefault(a => a.ModalOptions?.ContainsKey(_modalCustomId) == true);

        if (selectedModal == null) return null;

        _modalOptions = selectedModal.ModalOptions;
        return selectedModal.Action;
    }

    protected override Task<bool> CheckPreconditionsAsync(IActionSlashModal action) => Task.FromResult(true);

    protected override async Task PopulateParametersAsync(IActionSlashModal action)
    {
        if (_modalOptions.ContainsKey(_modalCustomId))
            await _modalOptions[_modalCustomId].FillParametersAsync(_idOptions);
    }

    protected override async Task RunActionAsync(IActionSlashModal action)
    {
        try
        {
            if (_modalOptions.ContainsKey(_modalCustomId))
                await _modalOptions[_modalCustomId].ModalCompleteAsync(_interaction);
        }
        catch (TimeoutException)
        {
            // ignored
        }
    }
}

public class ActionTextRunFactory(IServiceProvider services, ActionService actionService,
        AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, CommandInfo commandInfo,
        object[] parmValues)
    : ActionRunFactory<CommandInfo, IActionText>(services, actionService, asyncKeyedLocker, context, commandInfo)
{
    protected override string InteractionNameForLog => _interaction.Name;
    protected override ActionRunContext RunContext => ActionRunContext.Text;

    protected override IActionText GetAction() => _actionService.GetAll().OfType<IActionText>().FirstOrDefault(a => a.CommandName == _interaction.Name);

    protected override async Task PopulateParametersAsync(IActionText action)
    {
        await action.FillTextParametersAsync(parmValues);
    }
}