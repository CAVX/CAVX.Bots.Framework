using System;
using System.Collections.Generic;
using System.Text;
using Bot.Models;
using Discord;

namespace Bot.Processing
{
    public enum OutputFieldMessageType
    {
        Invalid,
        Protection,
        BreakingProtection,
        Modification,
        Result,
        Bonus
    }

    public class OutputFieldMessage
    {
        public IUser AffectedUserData { get; set; }
        public OutputFieldMessageType MessageType { get; set; }
        public string Message { get; set; }

        public OutputFieldMessage(IUser affectedUserData, OutputFieldMessageType messageType, string message)
        {
            AffectedUserData = affectedUserData;
            MessageType = messageType;
            Message = message;
        }

        public static implicit operator OutputFieldMessage((IUser AffectedUserData, OutputFieldMessageType messageType, string Message) args) => new(args.AffectedUserData, args.messageType, args.Message);
    }
}
