using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions;

public static class ListExtensions
{
    public static void AddMultiple<T>(this List<T> list, params T[] collection)
    {
        list.AddRange(collection);
    }

    public static List<T> Shuffle<T>(this List<T> list)
    {
        var count = list.Count;
        if (count is 0 or 1)
            return list;

        while (count > 1)
        {
            count--;
            int k = Random.Shared.Next(count + 1);
            (list[count], list[k]) = (list[k], list[count]);
        }

        return list;
    }

    public static IQueryable<T> Query<T>(this IQueryable<T> items, Expression<Func<T, bool>> where = null) where T : class
    {
        Func<T, bool> whereFunc = where?.Compile();

        IQueryable<T> query = items.AsQueryable();
        if (whereFunc != null)
            query = query.AsEnumerable().Where(whereFunc).AsQueryable();

        return query;
    }

    public static IQueryable<TRet> Query<T, TRet>(this IQueryable<T> items, Expression<Func<T, TRet>> select, Expression<Func<T, bool>> where = null) where T : class
    {
        Func<T, TRet> selectFunc = select.Compile();
        IQueryable<T> query = Query(items, where);

        return query.AsEnumerable().Select(selectFunc).AsQueryable();
    }

    public static async Task<IEnumerable<T>> WhereAsync<T>(this IEnumerable<T> items, Func<T, Task<bool>> predicate)
    {
        var itemTaskList = items.Select(item => new { Item = item, PredTask = predicate.Invoke(item) }).ToList();
        await Task.WhenAll(itemTaskList.Select(x => x.PredTask));
        return itemTaskList.Where(x => x.PredTask.Result).Select(x => x.Item);
    }

    public static IEnumerable<IEnumerable<T>> GetAllPossibleCombos<T>(this IEnumerable<IEnumerable<T>> objects)
    {
        IEnumerable<List<T>> combos = new List<List<T>> { new() };

        foreach (var innerList in objects)
        {
            combos = combos.SelectMany(combo => innerList.Select(innerObject =>
            {
                var listCopy = combo.ToList();

                if (innerObject != null)
                    listCopy.Add(innerObject);

                return listCopy;
            }).ToList());
        }

        // Remove combinations were all items are empty
        return combos.Where(c => c.Count > 0).ToList();
    }

    #region https://stackoverflow.com/questions/4140719/calculate-median-in-c-sharp

    /// <summary>
    /// Partitions the given list around a pivot element such that all elements on left of pivot are less than or equal to the pivot
    /// and the ones at thr right are greater than the pivot. This method can be used for sorting, N-order statistics such as
    /// as median finding algorithms.
    /// Pivot is selected randomly if random number generator is supplied else its selected as last element in the list.
    /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 171
    /// </summary>
    private static int Partition<T>(this IList<T> list, int start, int end, Random rnd = null) where T : IComparable<T>
    {
        if (rnd != null)
            list.Swap(end, rnd.Next(start, end + 1));

        var pivot = list[end];
        var lastLow = start - 1;
        for (var i = start; i < end; i++)
        {
            if (list[i].CompareTo(pivot) <= 0)
                list.Swap(i, ++lastLow);
        }
        list.Swap(end, ++lastLow);
        return lastLow;
    }

    /// <summary>
    /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
    /// Note: specified list would be mutated in the process.
    /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
    /// </summary>
    public static T NthOrderStatistic<T>(this IList<T> list, int n, Random rnd = null) where T : IComparable<T>
    {
        return NthOrderStatistic(list, n, 0, list.Count - 1, rnd);
    }

    private static T NthOrderStatistic<T>(this IList<T> list, int n, int start, int end, Random rnd) where T : IComparable<T>
    {
        while (true)
        {
            var pivotIndex = list.Partition(start, end, rnd);
            if (pivotIndex == n)
                return list[pivotIndex];

            if (n < pivotIndex)
                end = pivotIndex - 1;
            else
                start = pivotIndex + 1;
        }
    }

    public static void Swap<T>(this IList<T> list, int i, int j)
    {
        if (i == j)   //This check is not required but Partition function may make many calls so its for perf reason
            return;
        (list[j], list[i]) = (list[i], list[j]);
    }

    /// <summary>
    /// Note: specified list would be mutated in the process.
    /// </summary>
    public static T Median<T>(this IList<T> list) where T : IComparable<T>
    {
        return list.NthOrderStatistic((list.Count - 1) / 2);
    }

    public static double Median<T>(this IEnumerable<T> sequence, Func<T, double> getValue)
    {
        var list = sequence.Select(getValue).ToList();
        var mid = (list.Count - 1) / 2;
        return list.NthOrderStatistic(mid);
    }

    #endregion https://stackoverflow.com/questions/4140719/calculate-median-in-c-sharp
}