using Discord;

namespace CAVX.Bots.Framework.Models
{
    public interface IEmbedMetadata
    {
        string EmbedTitle { get; }
        Color EmbedColor { get; }
        string EmbedThumbnailUrl { get; }
    }

    public class EmbedMetadata(string title, Color color, string thumbnailUrl) : IEmbedMetadata
    {
        public string EmbedTitle { get; } = title;
        public Color EmbedColor { get; } = color;
        public string EmbedThumbnailUrl { get; } = thumbnailUrl;
    }
}