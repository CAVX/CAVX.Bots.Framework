using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Modules.Actions.Attributes;
using Bot.Services;
using Bot.Models;

namespace Bot.Modules.Actions.Core
{
    public class Info : BotCommandAction
    {
        public IUser _preTarget = null;

        [ActionParameterSlash(Order = 1, Name = "target", Description = "Choose another user to see their pet!", Required = false, Type = ApplicationCommandOptionType.User )]
        [ActionParameterText(Order = 1, Name = "target", Description = "Choose another user to see their pet.", Required = false, ParameterType = typeof(IUser))]
        public IUser TargetUser { get; set; }

        public override ActionGlobalSlashCommandProperties SlashCommandProperties => new()
        {
            Name = "info",
            Description = "Look to see what pet you or another user has!",
            FillParametersAsync = options =>
            {
                if (options != null)
                    TargetUser = ValueForSlashOption<IUser>(options, nameof(TargetUser));
                
                return Task.CompletedTask;
            }
        };

        public override List<ActionTextCommandProperties> TextCommandProperties => new()
        {
            new()
            {
                Name = "info", Aliases = new() { "pet", "view" },
                Summary = "Look to see what pet you or another user has!",
                ShowInHelp = true,
                FillParametersAsync = options =>
                {
                    if (options != null)
                        TargetUser = options[0] == null ? null : options[0] as IUser;

                    return Task.CompletedTask;
                }
            }
        };

        public override IActionMessageCommandProperties MessageCommandProperties => null;

        public override ActionGlobalUserCommandProperties UserCommandProperties => new()
        {
            Name = "View Pet",
            FillParametersAsync = (user) =>
            {
                _preTarget = user;
                return Task.CompletedTask;
            }
        };

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
            var user = Context.User;
            if (TargetUser == null)
                TargetUser = user;

            await Context.ReplyWithMessageAsync(EphemeralRule, embed: BuildCoinCountEmbed(user, TargetUser));
        }

        private Embed BuildCoinCountEmbed(IUser user, IUser targetUser)
        {
            var pet = Pet.GetAll().OrderBy(x => Guid.NewGuid()).FirstOrDefault();

            var fieldsBuilder = new List<EmbedFieldBuilder>
            {
                new EmbedFieldBuilder()
                {
                    Name = "Current Pet:",
                    Value = $"{pet.Name}",
                    IsInline = true
                }
            };

            var embed = new EmbedBuilder()
            {
                Description = $"Here's what I have on **{targetUser?.Username ?? "???"}**:",
                Color = Color.Gold,
                Timestamp = DateTime.Now,
                Fields = fieldsBuilder
            };

            return embed.Build();
        }
    }
}
