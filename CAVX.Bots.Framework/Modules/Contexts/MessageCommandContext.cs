﻿using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace CAVX.Bots.Framework.Modules.Contexts
{
    public class MessageCommandContext : SocketCommandContext
    {
        public string CommandInput { get; private set; }

        public MessageCommandContext(DiscordSocketClient client, SocketUserMessage msg, string input) : base(client, msg)
        {
            CommandInput = input;
        }
    }
}
