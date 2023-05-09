using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Services;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Processing;
using Microsoft.Extensions.DependencyInjection;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public class Collector : BotAction, IActionComponent
    {
        [ActionParameterComponent(Order = 1, Name = "idParams", Description = "Any additional parameters passed in from the message component's custom ID.", Required = false)]
        public object[] IdParams { get; set; } = null;

        [ActionParameterComponent(Order = 2, Name = "selectParams", Description = "Any additional parameters passed in from the message component's select box.", Required = false)]
        public object[] SelectParams { get; set; } = null;

        public Task FillComponentParametersAsync(string[] selectOptions, object[] idOptions)
        {
            IdParams = idOptions;
            SelectParams = selectOptions;

            return Task.CompletedTask;
        }

        public override EphemeralRule EphemeralRule => EphemeralRule.EphemeralOrFail;
        public override bool RestrictAccessToGuilds => true;
        public override bool ConditionalGuildsOnly => false;
        public override ActionAccessRule RequiredAccessRule => null;

        readonly IServiceScopeFactory _scopeFactory;
        readonly IContextService _contextService;
        readonly ActionService _actionService;

        public Collector(IServiceScopeFactory scopeFactory, IContextService contextService, ActionService actionService)
        {
            _scopeFactory = scopeFactory;
            _contextService = contextService;
            _actionService = actionService;
        }

        protected override Task<(bool Success, string Message)> CheckCustomPreconditionsAsync(ActionRunContext runContext)
        {
            _contextService.AddContext(this);

            return Task.FromResult((true, (string)null));
        }

        public override async Task RunAsync(ActionRunContext runContext)
        {
            IUser userData = Context.User;
            if (userData == null)
            {
                await Context.ReplyAsync(true, "I can't find your user record!");
                return;
            }
            if (Context.Message == null)
            {
                await Context.ReplyAsync(true, "I can't find that message!");
                return;
            }

            IUserMessage message = Context.Message;
            ulong messageId = message.Id;
            if (!_actionService.CollectorAvailable(messageId))
            {
                var newMessageId = message.ReferencedMessage?.Id ?? (message.Reference != null && message.Reference.MessageId.IsSpecified ? message.Reference.MessageId.Value : null);
                if (newMessageId.HasValue)
                    messageId = newMessageId.Value;
            }

            var (Result, FailureMessage, Builder) = await _actionService.FireCollectorAsync(userData, messageId, IdParams, SelectParams);
            if (Result != MessageResultCode.Success)
            {
                if (Builder == null)
                    await Context.ReplyAsync(true, FailureMessage ?? "Something went wrong!");
                else
                    await Context.ReplyBuilderAsync(_scopeFactory, Builder, true, this, messageId);

                return;
            }

            if (Builder != null)
                await Context.ReplyBuilderAsync(_scopeFactory, Builder, true, this, messageId);
            else
                await Context.ReplyAsync(true, "Got it. Thanks!");
        }
    }
}
