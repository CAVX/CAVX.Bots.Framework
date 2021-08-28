using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Modules.Contexts;
using Bot.Services;
using Bot.Models;

namespace Bot.Modules.Actions.Core
{
    public class Pin : BotCommandAction
    {
        public IMessage Message { get; set; }

        public override ActionGlobalSlashCommandProperties SlashCommandProperties => null;

        public override List<ActionTextCommandProperties> TextCommandProperties => new()
        {
            new()
            {
                Name = "pin this",
                Aliases = new() { "pin that" },
                FillParametersAsync = options =>
                {
                    Message = Context.Message?.ReferencedMessage;
                    return Task.CompletedTask;
                }
            }
        };

        public override ActionGlobalMessageCommandProperties MessageCommandProperties => new()
        {
            Name = "Pin This Message",
            FillParametersAsync = (message) =>
            {
                Message = message;
                return Task.CompletedTask;
            }
        };

        public override ActionGlobalUserCommandProperties UserCommandProperties => null;

        public override EphemeralRule EphemeralRule => EphemeralRule.EphemeralOrFallback;
        public override bool GuildsOnly => true;
        public override GuildPermissions? RequiredPermissions => null;

        IServiceScope _scope;
        RequestContextService _requestContextService;

        protected override Task<(bool Success, string Message)> CheckCustomPreconditionsAsync()
        {
            var guildResult = IsGameCommandAllowedInGuild();
            if (!guildResult.Success)
                return Task.FromResult(guildResult);

            _scope = ServiceProvider.CreateScope();
            _requestContextService = _scope.ServiceProvider.GetRequiredService<RequestContextService>();
            _requestContextService.AddContext(Context);

            return Task.FromResult(guildResult);
        }

        public override async Task RunAsync()
        {
            if (Message == null)
            {
                await Context.ReplyWithMessageAsync(EphemeralRule, "I couldn't find a message to pin!" + (Context is RequestCommandContext ? " Be sure you're replying to a message when you tell me this!" : ""));
                return;
            }

            //This won't resolve in the example - just here as another idea of what to do.
            //var (Success, Result) = await _pinService.PinAsync(Message, (ITextChannel)Message.Channel, false);
            (bool Success, string Message) result = (true, null);
            await Context.ReplyWithMessageAsync(EphemeralRule, result.Message ?? (result.Success ? "All done! It's pinned." : "Oh no! I couldn't pin it for some reason! Sorry!"));
        }
    }
}
