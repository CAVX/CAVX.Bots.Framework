using Discord;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Bot.Services;
using Bot.Models;

namespace Bot.Modules.Actions.Core.Components
{
    public abstract class PetShopBuy : BotComponentAction
    {
        public override EphemeralRule EphemeralRule => EphemeralRule.Permanent;

        public override bool GuildsOnly => true;

        public override GuildPermissions? RequiredPermissions => GuildPermissions.None.Modify(manageRoles: true);

        protected IServiceScope _scope;
        protected RequestContextService _requestContextService;

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
    }
}
