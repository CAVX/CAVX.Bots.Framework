using System;
using System.Collections.Generic;
using System.Text;

namespace CAVX.Bots.Framework.Utilities
{
    public class Cache<TKey, TValue>
    {
        private readonly Dictionary<TKey, CacheItem<TValue>> _cache = new();

        public void Store(TKey key, TValue value, TimeSpan expiresAfter)
        {
            _cache[key] = new CacheItem<TValue>(value, expiresAfter);
        }

        public TValue Get(TKey key)
        {
            if (!_cache.ContainsKey(key)) return default;
            var cached = _cache[key];
            if (DateTimeOffset.Now - cached.Created >= cached.ExpiresAfter)
            {
                _cache.Remove(key);
                return default;
            }
            return cached.Value;
        }
    }

    public class CacheItem<T>
    {
        public CacheItem(T value, TimeSpan expiresAfter)
        {
            Value = value;
            ExpiresAfter = expiresAfter;
        }
        public T Value { get; }
        internal DateTimeOffset Created { get; } = DateTimeOffset.Now;
        internal TimeSpan ExpiresAfter { get; }
    }
}
