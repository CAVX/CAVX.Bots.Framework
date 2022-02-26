using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
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

namespace CAVX.Bots.Framework.Processing
{
    public abstract class ActionRunFactory
    {
        public abstract Task RunActionAsync();

        public static ActionRunFactory Find(ActionService actionService, RequestContext context, SocketInteraction interaction)
        {
            if (interaction is SocketSlashCommand slashCommand)
                return new ActionSlashRunFactory(actionService, context, slashCommand);
            else if (interaction is SocketMessageCommand msgCommand)
                return new ActionMessageRunFactory(actionService, context, msgCommand);
            else if (interaction is SocketUserCommand userCommand)
                return new ActionUserRunFactory(actionService, context, userCommand);
            else if (interaction is SocketAutocompleteInteraction autocompleteInteraction)
                return new ActionAutocompleteResponseFactory(actionService, context, autocompleteInteraction);
            else if (interaction is SocketMessageComponent component)
            {
                if (component.Data.CustomId.StartsWith('#') || component.Data.CustomId.StartsWith('^')) //# = refresh component, ^ = refresh into new component
                    return new ActionRefreshRunFactory(actionService, context, component);
                else
                    return new ActionComponentRunFactory(actionService, context, component);
            }

            return null;
        }

        public static ActionRunFactory Find(ActionService actionService, RequestContext context, CommandInfo commandInfo, object[] parmValues) => new ActionTextRunFactory(actionService, context, commandInfo, parmValues);
    }


    public abstract class ActionRunFactory<TInteraction, TAction> : ActionRunFactory where TInteraction : class where TAction : BotAction
    {
        protected TInteraction _interaction;
        protected RequestContext _context;
        protected ActionService _actionService;

        public ActionRunFactory(ActionService actionService, RequestContext context, TInteraction interaction)
        {
            _context = context;
            _interaction = interaction;

            _actionService = actionService;
        }

        protected abstract string InteractionNameForLog { get; }

        protected abstract TAction GetAction();
        protected abstract Task PopulateParametersAsync(TAction action);
        protected abstract Task RunActionAsync(TAction action);

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
                    await Task.Delay(5000);
                }, ct);
            }

            var action = GetAction();
            if (action == null)
                throw new CommandInvalidException();

            action.Initialize(_context);

            if (_interaction is SocketInteraction si && _context is RequestInteractionContext ic)
                QueueDefer(action, si, ic);
            if (!await PopulateAndValidateParametersAsync(action))
                return;
            if (!await CheckPreconditionsAsync(action))
                return;

            RunAndHandleAction(action);
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
                if (!action.ValidateParameters<ActionParameterSlashAttribute>())
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
            var (Success, Message) = await action.CheckPreconditionsAsync();
            if (!Success)
            {
                await _context.ReplyAsync(EphemeralRule.EphemeralOrFallback, Message ?? "Something went wrong with using this command!").ConfigureAwait(false);
                return false;
            }
            return true;
        }

        private void RunAndHandleAction(TAction action)
        {
            _ = Task.Run(async () =>
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
            });
        }
    }

    public class ActionSlashRunFactory : ActionRunFactory<SocketSlashCommand, BotCommandAction>
    {
        protected override string InteractionNameForLog => _interaction.Data.Name;

        public ActionSlashRunFactory(ActionService actionService, RequestContext context, SocketSlashCommand interaction) : base(actionService, context, interaction) { }

        protected override BotCommandAction GetAction() => _actionService.GetAll().OfType<BotCommandAction>().FirstOrDefault(a => a.SlashCommandProperties != null && a.SlashCommandProperties.Name == _interaction.Data.Name);

        protected override async Task PopulateParametersAsync(BotCommandAction action)
        {
            if (action.SlashCommandProperties.FillParametersAsync != null)
                await action.SlashCommandProperties.FillParametersAsync(_interaction.Data.Options);
        }

        protected override async Task RunActionAsync(BotCommandAction action)
        {
            await action.RunAsync();
        }
    }

    public class ActionMessageRunFactory : ActionRunFactory<SocketMessageCommand, BotCommandAction>
    {
        protected override string InteractionNameForLog => _interaction.Data.Name;

        public ActionMessageRunFactory(ActionService actionService, RequestContext context, SocketMessageCommand interaction) : base(actionService, context, interaction) { }

        protected override BotCommandAction GetAction() => _actionService.GetAll().OfType<BotCommandAction>().FirstOrDefault(a => a.MessageCommandProperties != null && a.MessageCommandProperties.Name == _interaction.Data.Name);

        protected override async Task PopulateParametersAsync(BotCommandAction action)
        {
            if (action.MessageCommandProperties.FillParametersAsync != null)
                await action.MessageCommandProperties.FillParametersAsync(_interaction.Data.Message);
        }

        protected override async Task RunActionAsync(BotCommandAction action)
        {
            await action.RunAsync();
        }
    }

    public class ActionUserRunFactory : ActionRunFactory<SocketUserCommand, BotCommandAction>
    {
        protected override string InteractionNameForLog => _interaction.Data.Name;

        public ActionUserRunFactory(ActionService actionService, RequestContext context, SocketUserCommand interaction) : base(actionService, context, interaction) { }

        protected override BotCommandAction GetAction() => _actionService.GetAll().OfType<BotCommandAction>().FirstOrDefault(a => a.UserCommandProperties != null && a.UserCommandProperties.Name == _interaction.Data.Name);

        protected override async Task PopulateParametersAsync(BotCommandAction action)
        {
            if (action.UserCommandProperties.FillParametersAsync != null)
                await action.UserCommandProperties.FillParametersAsync(_interaction.Data.Member);
        }

        protected override async Task RunActionAsync(BotCommandAction action)
        {
            await action.RunAsync();
        }
    }

    public class ActionRefreshRunFactory : ActionRunFactory<SocketMessageComponent, BotCommandAction>
    {
        readonly string _commandTypeName;
        readonly object[] _idOptions;
        readonly bool _intoNew = false;

        protected override string InteractionNameForLog => _interaction.Data.CustomId;

        public ActionRefreshRunFactory(ActionService actionService, RequestContext context, SocketMessageComponent interaction) : base(actionService, context, interaction)
        {
            if (string.IsNullOrWhiteSpace(_interaction.Data.CustomId))
                throw new CommandInvalidException();

            //# = refresh component, ^ = refresh into new component
            _intoNew = interaction.Data.CustomId.First() == '^';

            var splitId = interaction.Data.CustomId[1..].Split('.');
            _commandTypeName = splitId[0];
            _idOptions = splitId.Skip(1).Cast<object>().ToArray();
        }

        protected override BotCommandAction GetAction() => _actionService.GetAll().OfType<BotCommandAction>().FirstOrDefault(a => a.CommandRefreshProperties != null && a.GetType().Name == _commandTypeName);

        protected override async Task PopulateParametersAsync(BotCommandAction action)
        {
            if (action.CommandRefreshProperties.FillParametersAsync != null)
            {
                var selectOptions = _interaction.Data.Values?.ToArray();
                await action.CommandRefreshProperties.FillParametersAsync(selectOptions, _idOptions);
            }
        }

        protected override async Task RunActionAsync(BotCommandAction action)
        {
            var (Success, Message) = await action.CommandRefreshProperties.CanRefreshAsync(_intoNew);
            if (!Success)
            {
                await _context.ReplyAsync(true, Message);
                return;
            }

            if (_intoNew)
            {
                var props = new MessageProperties();
                await action.CommandRefreshProperties.RefreshAsync(_intoNew, props);
                await _context.ReplyAsync(action.EphemeralRule.ToEphemeral() ? EphemeralRule.EphemeralOrFail : EphemeralRule.Permanent, props.Content.GetValueOrDefault(), embed: props.Embed.GetValueOrDefault(), embeds: props.Embeds.GetValueOrDefault(),
                    allowedMentions: props.AllowedMentions.GetValueOrDefault(), components: props.Components.GetValueOrDefault());
            }
            else
            {
                await _context.UpdateReplyAsync(msgProps => action.CommandRefreshProperties.RefreshAsync(_intoNew, msgProps).GetAwaiter().GetResult());
            }
        }
    }

    public class ActionComponentRunFactory : ActionRunFactory<SocketMessageComponent, BotComponentAction>
    {
        readonly string _commandTypeName;
        readonly object[] _idOptions;

        protected override string InteractionNameForLog => _interaction.Data.CustomId;

        public ActionComponentRunFactory(ActionService actionService, RequestContext context, SocketMessageComponent interaction) : base(actionService, context, interaction)
        {
            if (string.IsNullOrWhiteSpace(_interaction.Data.CustomId))
                throw new CommandInvalidException();

            //# = refresh component, ^ = refresh into new component
            var splitId = _interaction.Data.CustomId.Split('.');
            _commandTypeName = splitId[0];
            _idOptions = splitId.Skip(1).Cast<object>().ToArray();
        }

        protected override BotComponentAction GetAction() => _actionService.GetAll().OfType<BotComponentAction>().FirstOrDefault(a => a.GetType().Name == _commandTypeName);

        protected override async Task PopulateParametersAsync(BotComponentAction action)
        {
            var selectOptions = _interaction.Data.Values?.ToArray();
            await action.FillParametersAsync(selectOptions, _idOptions);
        }

        protected override async Task RunActionAsync(BotComponentAction action)
        {
            await action.RunAsync();
        }
    }

    public class ActionAutocompleteResponseFactory : ActionRunFactory<SocketAutocompleteInteraction, BotCommandAction>
    {
        protected override string InteractionNameForLog => _interaction.Data.CommandName;

        public ActionAutocompleteResponseFactory(ActionService actionService, RequestContext context, SocketAutocompleteInteraction interaction) : base(actionService, context, interaction) { }

        protected override BotCommandAction GetAction() => _actionService.GetAll().OfType<BotCommandAction>().FirstOrDefault(a => a.SlashCommandProperties != null && a.SlashCommandProperties.AutocompleteAsync != null && a.SlashCommandProperties.Name == _interaction.Data.CommandName);

        protected override Task<bool> CheckPreconditionsAsync(BotCommandAction action) => Task.FromResult(true);

        protected override Task PopulateParametersAsync(BotCommandAction action)
        {
            //if (action.SlashCommandProperties.FillParametersAsync != null)
                //await action.SlashCommandProperties.FillParametersAsync(_interaction.Data.Options);
            return Task.CompletedTask;
        }

        protected override async Task RunActionAsync(BotCommandAction action)
        {
            try
            {
                if (action.SlashCommandProperties.AutocompleteAsync.ContainsKey(_interaction.Data.Current.Name))
                    await action.SlashCommandProperties.AutocompleteAsync[_interaction.Data.Current.Name](_interaction);
            }
            catch (TimeoutException) {  }
        }
    }

    public class ActionTextRunFactory : ActionRunFactory<CommandInfo, BotCommandAction>
    {
        readonly object[] _parmValues;
        ActionTextCommandProperties _textProperties;
        protected override string InteractionNameForLog => _interaction.Name;

        public ActionTextRunFactory(ActionService actionService, RequestContext context, CommandInfo commandInfo, object[] parmValues) : base(actionService, context, commandInfo) { _parmValues = parmValues; }

        protected override BotCommandAction GetAction()
        {
            var action = _actionService.GetAll().OfType<BotCommandAction>().FirstOrDefault(s => s.TextCommandProperties != null && s.TextCommandProperties.Any(t => t.Name == _interaction.Name));

            _textProperties = action.TextCommandProperties.FirstOrDefault(t => t.Name == _interaction.Name);
            if (_textProperties == null)
                throw new CommandInvalidException();

            return action;
        }

        protected override async Task PopulateParametersAsync(BotCommandAction action)
        {
            if (_textProperties.FillParametersAsync != null)
                await _textProperties.FillParametersAsync(_parmValues);
        }

        protected override async Task RunActionAsync(BotCommandAction action)
        {
            await action.RunAsync();
        }
    }
}
