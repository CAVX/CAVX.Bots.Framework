using System;
using System.Collections.Generic;
using System.Linq;

namespace CAVX.Bots.Framework.Extensions;

public static class CollectionExtensions
{
    public static bool ExistsWithItems<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable == null)
            return false;

        if (enumerable.TryGetNonEnumeratedCount(out int count))
            return count > 0;

        return enumerable.Any();
    }

    public static T RandomElement<T>(this IEnumerable<T> sequence, Random random = null)
    {
        var enumerable = sequence.ToArray();
        var count = enumerable.Length;
        return count switch
        {
            0 => default,
            1 => enumerable[0],
            _ => enumerable[(random ?? Random.Shared).Next(0, count)]
        };
    }

    public static T RandomElement<T>(this T[] array, Random random = null)
    {
        return array[(random ?? Random.Shared).Next(array.Length)];
    }

    public static T RandomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, int> weightSelector, Random random = null)
    {
        var enumerable = sequence as T[] ?? sequence.ToArray();
        switch (enumerable.Length)
        {
            case 0:
                return default;
            case 1:
                return enumerable[0];
        }

        var weightedItems = (from weightedItem in enumerable select new { Value = weightedItem, Weight = weightSelector(weightedItem) }).ToList();

        int totalWeight = weightedItems.Sum(i => i.Weight);
        // The weight we are after...
        int itemWeightIndex = (random ?? Random.Shared).Next(0, totalWeight + 1);
        int currentWeightIndex = 0;

        foreach (var item in weightedItems)
        {
            currentWeightIndex += item.Weight;

            // If we've hit or passed the weight we are after for this item then it's the one we want....
            if (currentWeightIndex >= itemWeightIndex)
                return item.Value;
        }

        return default;
    }
}