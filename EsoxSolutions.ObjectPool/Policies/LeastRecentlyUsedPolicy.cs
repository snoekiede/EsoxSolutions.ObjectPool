using System.Collections.Concurrent;

namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Least Recently Used (LRU) pooling policy.
    /// Objects that haven't been used for the longest time are retrieved first.
    /// Best for: Preventing object staleness, ensuring all objects get exercised, connection keep-alive scenarios.
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public class LeastRecentlyUsedPolicy<T> : IPoolingPolicy<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, DateTimeOffset> _lastUsedTimes = new();
        private readonly object _lock = new();

        /// <inheritdoc/>
        public string PolicyName => "LRU";

        /// <inheritdoc/>
        public int Count => _lastUsedTimes.Count;

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _lastUsedTimes[item] = DateTimeOffset.UtcNow;
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            lock (_lock)
            {
                if (_lastUsedTimes.IsEmpty)
                {
                    item = default;
                    return false;
                }

                // Find the least recently used item
                var lruItem = _lastUsedTimes.OrderBy(kvp => kvp.Value).First();
                
                if (_lastUsedTimes.TryRemove(lruItem.Key, out _))
                {
                    item = lruItem.Key;
                    return true;
                }

                item = default;
                return false;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _lastUsedTimes.Clear();
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            return _lastUsedTimes.Keys.ToArray();
        }
    }
}
