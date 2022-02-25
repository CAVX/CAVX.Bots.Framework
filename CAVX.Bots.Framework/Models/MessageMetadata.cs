using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CAVX.Bots.Framework.Models
{
    public class MessageMetadata
    {
        public Embed Embed { get; set; }
        public byte[] ImageStreamBytes { get; set; }
        public bool ImageIsSpoiler { get; set; }
        public string ImageFileName { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
        public bool HasMentions { get; set; }
        public MessageComponent Components { get; set; }

        public MessageMetadata(Embed embed, byte[] imageStreamBytes, bool imageIsSpoiler, string imageFileName, string message, bool success, bool hasMentions, ComponentBuilder componentBuilder)
        {
            Embed = embed;
            ImageStreamBytes = imageStreamBytes;
            ImageIsSpoiler = imageIsSpoiler;
            ImageFileName = imageFileName;
            Message = message;
            Success = success;
            HasMentions = hasMentions;
            Components = componentBuilder?.Build();
        }
    }
}
