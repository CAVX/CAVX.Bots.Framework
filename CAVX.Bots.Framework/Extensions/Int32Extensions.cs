using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace CAVX.Bots.Framework.Extensions
{
    public static class Int32Extensions
    {
        public static string S(this int i)
        {
            if (i == 1 || i == -1)
                return "";
            else
                return "s";
        }
    }
}
