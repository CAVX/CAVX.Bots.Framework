using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Modules.Actions;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Services;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Extensions;
using System.Threading;
using Discord;
using AsyncKeyedLock;
using static System.Collections.Specialized.BitVector32;

namespace CAVX.Bots.Framework.Processing
{
    public abstract class ActionRunFactory
    {
        public abstract Task RunActionAsync();

        public static ActionRunFactory Find(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketInteraction interaction)
        {
            if (interaction is SocketSlashCommand slashCommand)
                return new ActionSlashRunFactory(services, actionService, asyncKeyedLocker, context, slashCommand);
            else if (interaction is SocketMessageCommand msgCommand)
                return new ActionMessageRunFactory(services, actionService, asyncKeyedLocker, context, msgCommand);
            else if (interaction is SocketUserCommand userCommand)
                return new ActionUserRunFactory(services, actionService, asyncKeyedLocker, context, userCommand);
            else if (interaction is SocketAutocompleteInteraction autocompleteInteraction)
                return new ActionAutocompleteResponseFactory(services, actionService, asyncKeyedLocker, context, autocompleteInteraction);
            else if (interaction is SocketModal modalInteraction)
                return new ActionModalResponseFactory(services, actionService, asyncKeyedLocker, context, modalInteraction);
            else if (interaction is SocketMessageComponent component)
                    return new ActionComponentRunFactory(services, actionService, asyncKeyedLocker, context, component);

            return null;
        }

        public static ActionRunFactory Find(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, CommandInfo commandInfo, object[] parmValues) => new ActionTextRunFactory(services, actionService, asyncKeyedLocker, context, commandInfo, parmValues);
    }


    public abstract class ActionRunFactory<TInteraction, TAction> : ActionRunFactory where TInteraction : class where TAction : IBotAction
    {
        protected IServiceProvider _services;
        protected TInteraction _interaction;
        protected RequestContext _context;
        protected ActionService _actionService;
        protected AsyncKeyedLocker<ulong> _asyncKeyedLocker;

        public ActionRunFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, TInteraction interaction)
        {
            _services = services;
            _context = context;
            _interaction = interaction;

            _actionService = actionService;
            _asyncKeyedLocker = asyncKeyedLocker;
        }

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
            var guid = Guid.NewGuid();
            Console.WriteLine($"[{guid}] starting");

            CancellationTokenSource ts = null;
            if (_context is RequestCommandContext)
            {
                ts = new();
                CancellationToken ct = ts.Token;
                _ = Task.Run(async () =>
                {
                    using IDisposable typingObject = _context is RequestCommandContext ? _context.Channel?.EnterTypingState() : null;
                    await Task.Delay(5000);
                }, ct);
            }

            var action = GetAction();
            if (action == null)
                throw new CommandInvalidException();

            action.Initialize(_context);

            Console.WriteLine($"[{guid}] action initialized");

            bool skipDefer = action.GetType().GetCustomAttributes(false).OfType<ActionNoDeferAttribute>().ExistsWithItems();
            bool useQueue = action.GetType().GetCustomAttributes(false).OfType<ActionUseQueueAttribute>().ExistsWithItems();

            if (_interaction is SocketInteraction si && _context is RequestInteractionContext ic && !skipDefer)
            {
                QueueDefer(action, si, ic);
                Console.WriteLine($"[{guid}] defer queued");
            }

            if (useQueue && _context.Guild != null)
            {

                Console.WriteLine($"[{guid}] waiting for lock");
                using (await _asyncKeyedLocker.LockAsync(_context.Guild.Id))
                {

                    Console.WriteLine($"[{guid}] locked");
                    if (!Utilities.TempDebug.False)
                        await Task.Delay(6000);
                    action = GetAction(); //refresh scope
                    Console.WriteLine($"[{guid}] scope refreshed");
                    action.Initialize(_context);
                    Console.WriteLine($"[{guid}] executing action");
                    await ExecuteActionAsync(action, ts);
                    Console.WriteLine($"[{guid}] done");
                }
            }
            else
            {
                Console.WriteLine($"[{guid}] no lock needed, executing");
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

        private void QueueDefer(TAction action, SocketInteraction si, RequestInteractionContext ic)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    double secondsToWait = 2.1d - (DateTime.UtcNow - si.CreatedAt.UtcDateTime).TotalSeconds;
                    if (secondsToWait > 2.1)
                        secondsToWait = 2.1;

                    if (secondsToWait > 0)
                        await Task.Delay((secondsToWait * 1000).IntLop(Math.Floor));

                    await ic.HadBeenAcknowledgedAsync(RequestAcknowledgeStatus.Acknowledged, async () => await si.DeferAsync(action.EphemeralRule.ToEphemeral()));
                }
                catch { }
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
            var (Success, Message) = await action.CheckPreconditionsAsync(RunContext);
            if (!Success)
            {
                await _context.ReplyAsync(EphemeralRule.EphemeralOrFallback, Message ?? "Something went wrong with using this command!").ConfigureAwait(false);
                return false;
            }
            return true;
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
                return;
            }
        }
    }

    public class ActionSlashRunFactory : ActionRunFactory<SocketSlashCommand, IActionSlash>
    {
        private IEnumerable<SocketSlashCommandDataOption> _subOptions;

        protected override string InteractionNameForLog => _interaction.Data.Name;
        protected override ActionRunContext RunContext => ActionRunContext.Slash;

        public ActionSlashRunFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketSlashCommand interaction) : base(services, actionService, asyncKeyedLocker, context, interaction) { }

        protected override IActionSlash GetAction()
        {
            var (SubCommandName, SubOptions) = _interaction.Data.Options.GetSelectedSubOption();

            if (SubCommandName != null)
            {
                _subOptions = SubOptions;
                return _actionService.GetAll().OfType<IActionSlashChild>().Where(a => a.Parent.CommandName == _interaction.Data.Name).OfType<IActionSlash>().Where(a => a.CommandName == SubCommandName).FirstOrDefault();
            }
            else
            {
                return _actionService.GetAll().OfType<IActionSlash>().Where(a => a.CommandName == _interaction.Data.Name).FirstOrDefault();
            }
        }

        protected override async Task PopulateParametersAsync(IActionSlash action)
        {
            await action.FillSlashParametersAsync(_subOptions ?? _interaction.Data.Options);
        }
    }

    public class ActionMessageRunFactory : ActionRunFactory<SocketMessageCommand, IActionMessage>
    {
        protected override string InteractionNameForLog => _interaction.Data.Name;
        protected override ActionRunContext RunContext => ActionRunContext.Message;

        public ActionMessageRunFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketMessageCommand interaction) : base(services, actionService, asyncKeyedLocker, context, interaction) { }

        protected override IActionMessage GetAction() => _actionService.GetAll().OfType<IActionMessage>().FirstOrDefault(a => a.CommandName == _interaction.Data.Name);

        protected override async Task PopulateParametersAsync(IActionMessage action)
        {
            await action.FillMessageParametersAsync(_interaction.Data.Message);
        }
    }

    public class ActionUserRunFactory : ActionRunFactory<SocketUserCommand, IActionUser>
    {
        protected override string InteractionNameForLog => _interaction.Data.Name;
        protected override ActionRunContext RunContext => ActionRunContext.User;

        public ActionUserRunFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketUserCommand interaction) : base(services, actionService, asyncKeyedLocker, context, interaction) { }

        protected override IActionUser GetAction() => _actionService.GetAll().OfType<IActionUser>().FirstOrDefault(a => a.CommandName == _interaction.Data.Name);

        protected override async Task PopulateParametersAsync(IActionUser action)
        {
            await action.FillUserParametersAsync(_interaction.Data.Member);
        }
    }

    public class ActionComponentRunFactory : ActionRunFactory<SocketMessageComponent, IActionComponent>
    {
        readonly string _commandTypeName;
        readonly object[] _idOptions;

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
            var selectOptions = _interaction.Data.Values?.ToArray();
            await action.FillComponentParametersAsync(selectOptions, _idOptions);
        }
    }

    public class ActionAutocompleteResponseFactory : ActionRunFactory<SocketAutocompleteInteraction, IActionSlashAutocomplete>
    {
        protected override string InteractionNameForLog => _interaction.Data.CommandName;

        public ActionAutocompleteResponseFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, SocketAutocompleteInteraction interaction) : base(services, actionService, asyncKeyedLocker, context, interaction) { }

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
            catch (TimeoutException) {  }
        }
    }

    public class ActionModalResponseFactory : ActionRunFactory<SocketModal, IActionSlashModal>
    {
        readonly string _modalCustomId;
        readonly object[] _idOptions;

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
            .Where(a => a.ModalOptions != null && a.ModalOptions.ContainsKey(_modalCustomId)).FirstOrDefault();

            if (selectedModal != null)
            {
                _modalOptions = selectedModal.ModalOptions;
                return selectedModal.Action;
            }

            return null;
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
            catch (TimeoutException) { }
        }
    }

    public class ActionTextRunFactory : ActionRunFactory<CommandInfo, IActionText>
    {
        readonly object[] _parmValues;
        protected override string InteractionNameForLog => _interaction.Name;
        protected override ActionRunContext RunContext => ActionRunContext.Text;

        public ActionTextRunFactory(IServiceProvider services, ActionService actionService, AsyncKeyedLocker<ulong> asyncKeyedLocker, RequestContext context, CommandInfo commandInfo, object[] parmValues) : base(services, actionService, asyncKeyedLocker, context, commandInfo)
        {
            _parmValues = parmValues;
        }

        protected override IActionText GetAction() => _actionService.GetAll().OfType<IActionText>().FirstOrDefault(a => a.CommandName == _interaction.Name);

        protected override async Task PopulateParametersAsync(IActionText action)
        {
            await action.FillTextParametersAsync(_parmValues);
        }
    }
}
