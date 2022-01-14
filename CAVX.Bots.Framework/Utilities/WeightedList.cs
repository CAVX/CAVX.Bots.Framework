using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Utilities
{
    public class WeightedList<T>
    {
        private class SortingNode
        {
            public int Weight { get; set; }
            public T Value { get; set; }
            public int TotalWeight { get; set; }

            public SortingNode(int weight, T value, int totalWeight)
            {
                Weight = weight;
                Value = value;
                TotalWeight = totalWeight;
            }
        }

        private readonly List<SortingNode> _list;

        public WeightedList(IEnumerable<T> items, Func<T, int> weightSelector)
        {
            _list = new();
            _list.Add(null);

            foreach (T item in items)
            {
                int weight = weightSelector(item);
                _list.Add(new SortingNode(weight, item, weight));
            }

            for (int i = _list.Count - 1; i > 1; i--)
            {
                _list[i >> 1].TotalWeight += _list[i].TotalWeight;
            }
        }

        public (bool Success, T Value) TryPop()
        {
            int randomWeight = ThreadSafeRandom.ThisThreadsRandom.Next(_list[1].TotalWeight);
            int i = 1;

            while (randomWeight >= _list[i].Weight)
            {
                randomWeight -= _list[i].Weight;
                i <<= 1;

                if (_list.Count <= i)
                    return (false, default);

                if (randomWeight >= _list[i].TotalWeight)
                {
                    randomWeight -= _list[i].TotalWeight;
                    i += 1;

                    if (_list.Count <= i)
                        return (false, default);
                }
            }

            int weight = _list[i].Weight;
            T item = _list[i].Value;

            _list[i].Weight = 0;

            while (i > 0)
            {
                _list[i].TotalWeight -= weight;
                i >>= 1;
            }

            return (true, item);
        }
    }
}
