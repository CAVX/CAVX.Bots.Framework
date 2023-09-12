using System;
using System.Collections.Generic;

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

            if (DateTimeOffset.Now - cached.Created < cached.ExpiresAfter) return cached.Value;

            _cache.Remove(key);
            return default;
        }
    }

    public class CacheItem<T>(T value, TimeSpan expiresAfter)
    {
        public T Value { get; } = value;
        internal DateTimeOffset Created { get; } = DateTimeOffset.Now;
        internal TimeSpan ExpiresAfter { get; } = expiresAfter;
    }
}