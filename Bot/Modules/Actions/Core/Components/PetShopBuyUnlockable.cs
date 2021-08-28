using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Modules.Actions.Attributes;
using Bot.Models;

namespace Bot.Modules.Actions.Core.Components
{
    public class PetShopBuyUnlockable : PetShopBuy
    {
        [ActionParameterComponent(Order = 1, Name = "target", Description = "Choose an unlockable to buy!", Required = true)]
        public int? UnlockableItemId { get; set; }

        public override Task FillParametersAsync(string[] selectOptions, object[] idOptions)
        {
            if (selectOptions != null && selectOptions.Any())
                UnlockableItemId = Convert.ToInt32(selectOptions.First());
            else if (idOptions != null && idOptions.Any())
                UnlockableItemId = Convert.ToInt32(idOptions.First());

            return Task.CompletedTask;
        }

        public override async Task RunAsync()
        {
            var unlockablesForSale = Unlockable.GetAll();

            var itemForSale = unlockablesForSale.FirstOrDefault(i => i.Id == UnlockableItemId.Value);
            if (itemForSale == null)
            {
                await Context.ReplyWithMessageAsync(true, "I can't find that for purchase!");
                return;
            }

            if (itemForSale.Cost > 200)
            {
                await Context.ReplyWithMessageAsync(true, "Your server can't afford that!");
                return;
            }

            await Context.ReplyWithMessageAsync(EphemeralRule, embed: BuildMarketPurchaseEmbed(itemForSale));
        }

        private Embed BuildMarketPurchaseEmbed(Unlockable itemPurchased)
        {
            var fieldEmbed = new List<EmbedFieldBuilder>()
            {
                new EmbedFieldBuilder() { Name = "Here's what you bought:", Value = $"*{itemPurchased.Name}*" }
            };

            if (!string.IsNullOrWhiteSpace(itemPurchased.Description))
            {
                fieldEmbed.Add(new EmbedFieldBuilder() { Name = "Here's what this does:", Value = $"*\"{itemPurchased.Description}\"*" });
            }

            var embed = new EmbedBuilder()
            {
                Title = "CONGRATULATIONS!",
                Description = "You bought something for your server!",
                Color = Color.Gold,
                Timestamp = DateTime.Now,
                Fields = fieldEmbed
            };

            return embed.Build();
        }
    }
}
