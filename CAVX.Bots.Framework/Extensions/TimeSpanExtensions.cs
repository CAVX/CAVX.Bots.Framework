using System;
using System.Linq;

namespace CAVX.Bots.Framework.Extensions;

public static class TimeSpanExtensions
{
    private enum TimeSpanElement
    {
        Second,
        Minute,
        Hour,
        Day,
        Week,
        Month,
        Year
    }

    public static string ToFriendlyString(this TimeSpan timeSpan, int maxNrOfElements)
    {
        int years = timeSpan.Days >= 365 ? ((double)timeSpan.Days / 365).IntLop(Math.Floor) : 0;
        int months = timeSpan.Days >= 30 ? ((double)timeSpan.Days / 30).IntLop(Math.Floor) : 0;
        int weeks = timeSpan.Days >= 7 ? ((double)timeSpan.Days / 7).IntLop(Math.Floor) : 0;
        int days = timeSpan.Days % 7;

        maxNrOfElements = Math.Max(Math.Min(maxNrOfElements, 5), 1);
        var parts = new (TimeSpanElement Element, int Quantity)[]
        {
            (TimeSpanElement.Year, years),
            (TimeSpanElement.Month, months),
            (TimeSpanElement.Week, weeks),
            (TimeSpanElement.Day, days),
            (TimeSpanElement.Hour, timeSpan.Hours),
            (TimeSpanElement.Minute, timeSpan.Minutes),
            (TimeSpanElement.Second, timeSpan.Seconds)
        }.SkipWhile(i => i.Quantity <= 0).Take(maxNrOfElements).ToArray();

        if (!parts.Any())
            return "moments";

        var stringParts = parts.Where(p => p.Quantity != 0).Select(p => $"{p.Quantity} {p.Element.ToString().ToLower()}{p.Quantity.S()}").ToArray();
        for (int i = 0; i < stringParts.Length; i++)
        {
            if (stringParts.Length - 2 == i)
                stringParts[i] += stringParts.Length == 2 ? " and " : ", and ";
            else if (stringParts.Length - 1 != i)
                stringParts[i] += ", ";
        }

        return string.Concat(stringParts);
    }
}