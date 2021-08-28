using Bot.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Modules.Actions.Core.Components
{
    public class PetShopBuySpecialItem : PetShopBuy
    {
        protected override async Task<(bool Success, string Message)> CheckCustomPreconditionsAsync() => await base.CheckCustomPreconditionsAsync();

        public override async Task RunAsync()
        {
            var user = Context.User;
            var specialItem = Item.GetAll().FirstOrDefault(i => i.Special);

            await Context.ReplyWithMessageAsync(EphemeralRule, $"You just bought {specialItem.Name}!");
        }
    }
}
