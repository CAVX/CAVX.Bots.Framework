using Discord;

namespace CAVX.Bots.Framework.Extensions
{
    public static class IMessageExtensions
    {
        public static bool HasMessageReferenceInSameChannel(this IMessage message)
            => message.Reference?.MessageId.IsSpecified == true && message.Reference.ChannelId == message.Channel.Id;
    }
}