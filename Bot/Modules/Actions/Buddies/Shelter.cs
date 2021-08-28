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
using Bot.Modules.Actions.Core.Components;
using Bot.Modules.Contexts;
using Bot.Services;
using Bot.Models;

namespace Bot.Modules.Actions.Buddies
{
    public class Shelter : BotCommandAction
    {
        [ActionParameterComponent(Order = 1, Name = "start", Description = "The record number to start from.", Required = false)]
        public int Start { get; set; } = 1;

        public override ActionGlobalSlashCommandProperties SlashCommandProperties => new()
        {
            Name = "shelter",
            Description = "See which pets are in the shelter, or take one out!"
        };

        public override List<ActionTextCommandProperties> TextCommandProperties => new()
        {
            new()
            {
                Name = "shelter",
                Summary = "See which pets are in the shelter, or take one out!",
                ShowInHelp = true
            }
        };

        public override ActionCommandRefreshProperties CommandRefreshProperties => new()
        {
            FillParametersAsync = (selectOptions, idOptions) =>
            {
                if (idOptions != null && idOptions.Any())
                    Start = Convert.ToInt32(idOptions[0]);

                return Task.CompletedTask;
            },
            CanRefreshAsync = CanPaginateAsync,
            RefreshAsync = PaginateMessageAsync
        };

        public override IActionMessageCommandProperties MessageCommandProperties => null;
        public override IActionUserCommandProperties UserCommandProperties => null;

        public override EphemeralRule EphemeralRule => EphemeralRule.EphemeralOrFail;
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
            var pets = Pet.GetAll();

            Embed embed = BuildShelterEmbed(pets);
            MessageComponent component = BuildShelterComponent(Start, Context.User, pets);
            await Context.ReplyWithMessageAsync(EphemeralRule, embed: embed, components: component);
        }

        public Task<(bool, string)> CanPaginateAsync(bool intoNew) => Task.FromResult((true, (string)null));

        public Task PaginateMessageAsync(bool intoNew, MessageProperties msgProps)
        {
            var pets = Pet.GetAll();

            Embed embed = BuildShelterEmbed(pets);
            MessageComponent component = BuildShelterComponent(Start, Context.User, pets);
            msgProps.Embed = embed;
            msgProps.Components = component;
            return Task.CompletedTask;
        }

        private Embed BuildShelterEmbed(List<Pet> pets)
        {
            bool empty = pets == null || !pets.Any();

            return new EmbedBuilder()
            {
                Title = "SHELTER!",
                Description = (empty ? "No one trusts me with their pets???" : "My shelter is very legitimate! I'm certified!") + Environment.NewLine + "You can put your current pet into the shelter. Or, take another user's pet!",
                Color = Color.LightOrange,
                Timestamp = DateTime.Now
            }.Build();
        }

        private MessageComponent BuildShelterComponent(int start, SocketUser user, List<Pet> pets)
        {
            bool personalized = Context is not RequestCommandContext;

            bool empty = pets == null || !pets.Any();
            int count = start;
            if (empty)
                return null;

            var builder = new ComponentBuilder();

            List<SelectMenuOptionBuilder> options = new();
            foreach (var pet in pets)
                options.Add(new SelectMenuOptionBuilder($"{pet.Name}", pet.Id.ToString(), $"A nice {pet.Name}", Emoji.Parse(pet.Emoji)));

            int truncated = 0;
            int limit = 24;
            if (options.Count > limit)
            {
                truncated = options.Count - limit;
                options = options.Take(limit).ToList();
            }

            string swapText = "Put your pet into the shelter";
            if (swapText != null)
                builder.WithButton(swapText, $"{nameof(Components.ShelterTake)}.", ButtonStyle.Primary, Emoji.Parse("📦"), row: 0);

            if (options.Any())
                builder.WithSelectMenu("Shelter Pets", nameof(Components.ShelterTake), options, $"Take a buddy for 25 coins{(truncated > 0 ? $" (+ {truncated} more)" : "")}", row: 1);

            bool prevEnabled = start > 1;
            bool nextEnabled = truncated > 0;
            if (personalized && (prevEnabled || nextEnabled))
            {
                builder.WithButton("◀️", $"#{GetType().Name}.{(start - limit < 1 ? 1 : start - limit)}", ButtonStyle.Secondary, disabled: !prevEnabled, row: 2)
                    .WithButton("▶️", $"#{GetType().Name}.{start + limit}", ButtonStyle.Secondary, disabled: !nextEnabled, row: 2);
            }
            else if (nextEnabled)
                builder.WithButton("Show More", $"^{GetType().Name}.{start + limit}", ButtonStyle.Secondary, disabled: !nextEnabled, row: 1);

            return builder.Build();
        }
    }
}
