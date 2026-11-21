namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Last-In-First-Out (LIFO) pooling policy using a stack.
    /// Most recently returned objects are retrieved first.
    /// Best for: Cache locality, temporal locality patterns.
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public class LifoPoolingPolicy<T> : IPoolingPolicy<T> where T : notnull
    {
        private readonly List<T> _items = [];
        private readonly object _lock = new();

        /// <inheritdoc/>
        public string PolicyName => "LIFO";

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count;
                }
            }
        }

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            lock (_lock)
            {
                _items.Add(item);
            }
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            lock (_lock)
            {
                var lastIndex = _items.Count - 1;
                if (lastIndex < 0)
                {
                    item = default;
                    return false;
                }

                item = _items[lastIndex];
                _items.RemoveAt(lastIndex);
                return true;
            }
        }

        /// <inheritdoc/>
        public bool TryTake(Func<T, bool> predicate, out T? item)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            lock (_lock)
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    var candidate = _items[i];
                    if (predicate(candidate))
                    {
                        item = candidate;
                        _items.RemoveAt(i);
                        return true;
                    }
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
                _items.Clear();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            lock (_lock)
            {
                var snapshot = new T[_items.Count];
                for (int i = 0; i < _items.Count; i++)
                {
                    snapshot[i] = _items[_items.Count - 1 - i];
                }
                return snapshot;
            }
        }
    }
}
