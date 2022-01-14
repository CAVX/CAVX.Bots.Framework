using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CAVX.Bots.Framework.Utilities;

namespace CAVX.Bots.Framework.Extensions
{
    public static class CollectionExtensions
    {
        public static T RandomElement<T>(this IEnumerable<T> sequence, Random random = null)
        {
            var count = sequence.Count();
            if (count == 0)
                return default;
            else if (count == 1)
                return sequence.First();

            return sequence.ElementAt((random ?? ThreadSafeRandom.ThisThreadsRandom).Next(0, count));
        }

        public static T RandomElement<T>(this T[] array, Random random = null)
        {
            return array[(random ?? ThreadSafeRandom.ThisThreadsRandom).Next(array.Length)];
        }

        public static T RandomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, int> weightSelector, Random random = null)
        {
            var count = sequence.Count();
            if (count == 0)
                return default;
            else if (count == 1)
                return sequence.First();

            var weightedItems = (from weightedItem in sequence select new { Value = weightedItem, Weight = weightSelector(weightedItem) }).ToList();

            int totalWeight = weightedItems.Sum(i => i.Weight);
            // The weight we are after...
            int itemWeightIndex = (random ?? ThreadSafeRandom.ThisThreadsRandom).Next(0, totalWeight + 1);
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
}
