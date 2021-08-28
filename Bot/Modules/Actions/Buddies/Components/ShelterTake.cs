using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Modules.Actions.Attributes;
using Bot.Modules.Contexts;
using Bot.Services;
using Bot.Models;

namespace Bot.Modules.Actions.Buddies.Components
{
    public class ShelterTake : BotComponentAction
    {
        [ActionParameterComponent(Order = 1, Name = "target", Description = "Choose a pet to take! Leave blank to put your own pet in.", Required = false)]
        public int? TargetPetId { get; set; }

        public override EphemeralRule EphemeralRule => EphemeralRule.Permanent;

        public override bool GuildsOnly => true;

        public override GuildPermissions? RequiredPermissions => null;

        protected IServiceScope _scope;
        protected RequestContextService _requestContextService;

        public override Task FillParametersAsync(string[] selectOptions, object[] idOptions)
        {
            if (idOptions != null && idOptions.Any() && idOptions[0] != null && idOptions[0].ToString().Length > 0)
                TargetPetId = Convert.ToInt32(idOptions[0]);
            else if (selectOptions != null && selectOptions.Any() && selectOptions[0] != null && selectOptions[0].ToString().Length > 0)
                TargetPetId = Convert.ToInt32(selectOptions[0]);

            return Task.CompletedTask;
        }

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
            Pet selectedPet = null;
            if (TargetPetId.HasValue)
            {
                selectedPet = Pet.GetAll().FirstOrDefault(p => p.Id == TargetPetId.Value);
                if (selectedPet == null)
                    await Context.ReplyWithMessageAsync(true, "I can't find that pet!");
            }

            if (TargetPetId.HasValue)
                await Context.ReplyWithMessageAsync(EphemeralRule, $"You just adopted a {selectedPet.Name}! {selectedPet.Emoji}");
            else
                await Context.ReplyWithMessageAsync(EphemeralRule, $"You just put your pet into the shelter...");
        }
    }
}
