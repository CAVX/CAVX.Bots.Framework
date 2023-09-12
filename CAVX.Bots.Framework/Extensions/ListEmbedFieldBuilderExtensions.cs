using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAVX.Bots.Framework.Extensions
{
    public static class ListEmbedFieldBuilderExtensions
    {
        public static void Add(this List<EmbedFieldBuilder> fieldEmbed, string name, string content, bool appendIfTooLong = true, bool isInline = false)
        {
            bool tooLong = false;
            if (content.Length > 1024)
            {
                tooLong = true;
                content = string.Concat(content.AsSpan(0, 1021), "...");
            }

            if (name != null && tooLong && appendIfTooLong)
                name += " (truncated)";

            fieldEmbed.Add(new EmbedFieldBuilder
            {
                Name = name,
                Value = content,
                IsInline = isInline
            });
        }

        public static void Add(this List<EmbedFieldBuilder> fieldEmbed, string name, IEnumerable<string> rows, bool appendIfTooLong = true, bool isInline = false)
        {
            bool tooLong = false;
            int rowsUsed = 0;
            StringBuilder sb = new();
            foreach (var row in rows)
            {
                if (sb.Length + Environment.NewLine.Length + row.Length > 1024)
                {
                    tooLong = true;
                    break;
                }

                if (sb.Length != 0)
                    sb.Append(Environment.NewLine);

                sb.Append(row);
                rowsUsed++;
            }

            if (!rows.TryGetNonEnumeratedCount(out var rowCount))
                rowCount = rows.Count();

            if (name != null && tooLong && appendIfTooLong)
                name += $" (+ {rowCount - rowsUsed} more)";

            fieldEmbed.Add(new EmbedFieldBuilder
            {
                Name = name,
                Value = sb.ToString(),
                IsInline = isInline
            });
        }
    }
}