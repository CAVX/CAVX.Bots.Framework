using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Modules.Actions.Attributes;
using CAVX.Bots.Framework.Processing;
using CAVX.Bots.Framework.Services;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public class Collector(IServiceScopeFactory scopeFactory, IContextService contextService,
            ActionService actionService)
        : BotAction, IActionComponent
    {
        [ActionParameterComponent(Order = 1, Name = "idParams", Description = "Any additional parameters passed in from the message component's custom ID.", Required = false)]
        public object[] IdParams { get; set; }

        [ActionParameterComponent(Order = 2, Name = "selectParams", Description = "Any additional parameters passed in from the message component's select box.", Required = false)]
        public object[] SelectParams { get; set; }

        public Task FillComponentParametersAsync(object[] selectOptions, object[] idOptions)
        {
            IdParams = idOptions;
            SelectParams = selectOptions;

            return Task.CompletedTask;
        }

        public override EphemeralRule EphemeralRule => EphemeralRule.EphemeralOrFail;
        public override bool RestrictAccessToGuilds => true;
        public override bool ConditionalGuildsOnly => false;
        public override ActionAccessRule RequiredAccessRule => null;

        protected override Task<(bool Success, string Message)> CheckCustomPreconditionsAsync(ActionRunContext runContext)
        {
            contextService.AddContext(this);

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
            if (!actionService.CollectorAvailable(messageId))
            {
                var newMessageId = message.ReferencedMessage?.Id ?? (message.Reference?.MessageId.IsSpecified == true ? message.Reference.MessageId.Value : null);
                if (newMessageId.HasValue)
                    messageId = newMessageId.Value;
            }

            var (result, failureMessage, builder) = await actionService.FireCollectorAsync(userData, messageId, IdParams, SelectParams);
            if (result != MessageResultCode.Success)
            {
                if (builder == null)
                    await Context.ReplyAsync(true, failureMessage ?? "Something went wrong!");
                else
                    await Context.ReplyBuilderAsync(scopeFactory, builder, true, this, messageId);

                return;
            }

            if (builder != null)
                await Context.ReplyBuilderAsync(scopeFactory, builder, true, this, messageId);
            else
                await Context.ReplyAsync(true, "Got it. Thanks!");
        }
    }
}