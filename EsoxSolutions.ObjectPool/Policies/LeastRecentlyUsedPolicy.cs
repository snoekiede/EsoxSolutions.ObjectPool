using EsoxSolutions.ObjectPool.Infrastructure;

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
        private readonly Dictionary<T, (DateTimeOffset Timestamp, long Sequence)> _lastUsedTimes =
            new(ReferenceOrValueEqualityComparer<T>.Instance);
        private readonly object _lock = new();
        private long _sequence;

        /// <inheritdoc/>
        public string PolicyName => "LRU";

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _lastUsedTimes.Count;
                }
            }
        }

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            lock (_lock)
            {
                _lastUsedTimes[item] = (DateTimeOffset.UtcNow, ++_sequence);
            }
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            return TryTake(_ => true, out item);
        }

        /// <inheritdoc/>
        public bool TryTake(Func<T, bool> predicate, out T? item)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            lock (_lock)
            {
                if (_lastUsedTimes.Count == 0)
                {
                    item = default;
                    return false;
                }

                T? lruKey = default;
                bool found = false;
                var lruEntry = (Timestamp: DateTimeOffset.MaxValue, Sequence: long.MaxValue);

                foreach (var kvp in _lastUsedTimes)
                {
                    if (!predicate(kvp.Key))
                    {
                        continue;
                    }

                    if (!found ||
                        kvp.Value.Timestamp < lruEntry.Timestamp ||
                        (kvp.Value.Timestamp == lruEntry.Timestamp && kvp.Value.Sequence < lruEntry.Sequence))
                    {
                        lruEntry = kvp.Value;
                        lruKey = kvp.Key;
                        found = true;
                    }
                }

                if (found && lruKey is not null && _lastUsedTimes.Remove(lruKey))
                {
                    item = lruKey;
                    return true;
                }

                item = default;
                return false;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            lock (_lock)
            {
                _lastUsedTimes.Clear();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            lock (_lock)
            {
                return [.. _lastUsedTimes.Keys];
            }
        }
    }
}
