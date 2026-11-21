namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// First-In-First-Out (FIFO) pooling policy using a queue.
    /// Objects are retrieved in the order they were returned to the pool.
    /// Best for: Fair scheduling, round-robin distribution, aging prevention.
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public class FifoPoolingPolicy<T> : IPoolingPolicy<T> where T : notnull
    {
        private readonly LinkedList<T> _queue = new();
        private readonly object _lock = new();

        /// <inheritdoc/>
        public string PolicyName => "FIFO";

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            lock (_lock)
            {
                _queue.AddLast(item);
            }
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            lock (_lock)
            {
                var first = _queue.First;
                if (first is null)
                {
                    item = default;
                    return false;
                }

                item = first.Value;
                _queue.RemoveFirst();
                return true;
            }
        }

        /// <inheritdoc/>
        public bool TryTake(Func<T, bool> predicate, out T? item)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            lock (_lock)
            {
                for (var node = _queue.First; node is not null; node = node.Next)
                {
                    if (predicate(node.Value))
                    {
                        item = node.Value;
                        _queue.Remove(node);
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
                _queue.Clear();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            lock (_lock)
            {
                return [.. _queue];
            }
        }
    }
}
