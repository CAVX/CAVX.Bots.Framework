using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Framework.Extensions;
using Bot.Modules.Actions.Attributes;
using Bot.Modules.Contexts;
using Bot.Services;
using Bot.TypeReaders;
using Bot.Models;

namespace Bot.Modules.Actions
{
    public class Collector : BotComponentAction
    {
        [ActionParameterComponent(Order = 1, Name = "idParams", Description = "Any additional parameters passed in from the message component's custom ID.", Required = false)]
        public object[] IdParams { get; set; } = null;

        [ActionParameterComponent(Order = 2, Name = "selectParams", Description = "Any additional parameters passed in from the message component's select box.", Required = false)]
        public object[] SelectParams { get; set; } = null;

        public override EphemeralRule EphemeralRule => EphemeralRule.EphemeralOrFail;

        public override bool GuildsOnly => true;

        public override GuildPermissions? RequiredPermissions => null;

        protected IServiceScope _scope;
        protected RequestContextService _requestContextService;
        protected ActionService _actionService;

        public override Task FillParametersAsync(string[] selectOptions, object[] idOptions)
        {
            IdParams = idOptions;
            SelectParams = selectOptions;

            return Task.CompletedTask;
        }

        protected override Task<(bool Success, string Message)> CheckCustomPreconditionsAsync()
        {
            _scope = ServiceProvider.CreateScope();
            _requestContextService = _scope.ServiceProvider.GetRequiredService<RequestContextService>();
            _actionService = _scope.ServiceProvider.GetRequiredService<ActionService>();
            _requestContextService.AddContext(Context);

            return Task.FromResult((true, (string)null));
        }

        public override async Task RunAsync()
        {
            IUser userData = Context.User;
            if (userData == null)
            {
                await Context.ReplyWithMessageAsync(true, "You're not playing!");
                return;
            }
            if (Context.Message == null)
            {
                await Context.ReplyWithMessageAsync(true, "I can't find that message!");
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

            var (Success, Builder) = await _actionService.FireCollectorAsync(userData, messageId, IdParams, SelectParams);
            if (!Success)
            {
                if (Builder == null)
                    await Context.ReplyWithMessageAsync(true, "Something went wrong!");
                else
                    await Context.ReplyBuilderAsync(_scope.ServiceProvider, Builder, true);

                return;
            }

            if (Builder != null)
                await Context.ReplyBuilderAsync(_scope.ServiceProvider, Builder, true, messageId);
            else
                await Context.ReplyWithMessageAsync(true, "Got it. Thanks!");
        }
    }
}
