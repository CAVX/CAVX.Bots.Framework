using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions
{
    public static class IMessageExtensions
    {
        public static bool HasMessageReferenceInSameChannel(this IMessage message)
            => message.Reference != null && message.Reference.MessageId.IsSpecified && message.Reference.ChannelId == message.Channel.Id;
    }
}
