using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Bot.Modules.Contexts;
using Bot.Models;


namespace Bot.Services
{

    public class RequestContextService
    {
        public RequestContext Context { get; set; }

        //DI
        private readonly IServiceProvider _services;

        public RequestContextService(IServiceProvider services)
        {
            _services = services;
        }

        public void Initialize()
        {
        }

        public void AddContext(RequestContext context)
        {
            Context = context;
        }

        public void AddContext(SocketCommandContext context)
        {
            Context = new RequestCommandContext(context);
        }

        public void AddContext(SocketSlashCommand interaction, DiscordSocketClient client)
        {
            Context = new RequestInteractionContext(interaction, client);
        }
    }
}
