using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Modules.Actions.Core.Components;
using Bot.Services;
using Discord.WebSocket;

namespace Bot.Modules.Actions.Core
{
    public class PetShop : BotCommandAction
    {
        public override ActionGlobalSlashCommandProperties SlashCommandProperties => new()
        {
            Name = "shop",
            Description = "Shop for items for your pets!"
        };

        public override List<ActionTextCommandProperties> TextCommandProperties => new()
        {
            new()
            {
                Name = "shop", Aliases = new() { "pet shop", "store", "pet store" },
                Summary = "Shop for items for your pets!",
                ShowInHelp = true
            }
        };

        public override IActionMessageCommandProperties MessageCommandProperties => null;
        public override IActionUserCommandProperties UserCommandProperties => null;

        public override EphemeralRule EphemeralRule => EphemeralRule.Permanent;
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
            var messageParts = BuildPetShopMessageParts(user);
            await Context.ReplyWithMessageAsync(EphemeralRule, embed: messageParts.Embed, components: messageParts.Component);
        }

        private (Embed Embed, MessageComponent Component) BuildPetShopMessageParts(SocketUser user)
        {
            var fieldEmbed = new List<EmbedFieldBuilder>();
            var specialItem = Item.GetAll().FirstOrDefault(i => i.Special);
            var unlockablesForSale = Unlockable.GetAll();

            fieldEmbed.Add(new EmbedFieldBuilder()
            {
                Name = "Your server's money:",
                Value = "💵 $200"
            });

            var embed = new EmbedBuilder()
            {
                Title = "PET SHOP",
                Description = "Hey! Want to buy an item that you can gift to a user? Check out the options for sale below! Only mods can buy them!",
                Color = Color.Gold,
                Timestamp = DateTime.Now,
                Fields = fieldEmbed
            };

            var builder = new ComponentBuilder()
                .WithButton($"{specialItem.Name} (cost: ${specialItem.Cost})", nameof(PetShopBuySpecialItem), ButtonStyle.Secondary, row: 0);

            if (unlockablesForSale.Any())
            {
                List<SelectMenuOptionBuilder> options = unlockablesForSale.Select(u => new SelectMenuOptionBuilder($"{u.Name}", u.Id.ToString(), $"Cost: ${u.Cost}", new Emoji("🛒")))
                    .OrderBy(o => Convert.ToInt32(o.Value)).ToList();

                int truncated = 0;
                if (options.Count >= 25)
                {
                    truncated = options.Count - 24;
                    options = options.Take(24).ToList();
                }

                builder.WithSelectMenu("Unlockables", nameof(PetShopBuyUnlockable), options, $"Select an unlockable to buy{(truncated > 0 ? $" (+ {truncated} more)" : "")}", row: 3);
            }

            return (embed.Build(), builder.Build());
        }
    }
}
