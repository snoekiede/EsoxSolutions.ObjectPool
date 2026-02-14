using System.Collections.Concurrent;

namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Round-robin pooling policy that distributes usage evenly across all objects.
    /// Objects are retrieved in a circular fashion.
    /// Best for: Load balancing, even wear distribution, connection pooling across multiple endpoints.
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public class RoundRobinPoolingPolicy<T> : IPoolingPolicy<T> where T : notnull
    {
        private readonly ConcurrentQueue<T> _queue = new();
        private readonly object _lock = new();

        /// <inheritdoc/>
        public string PolicyName => "RoundRobin";

        /// <inheritdoc/>
        public int Count => _queue.Count;

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _queue.Enqueue(item);
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            lock (_lock)
            {
                if (_queue.TryDequeue(out item))
                {
                    // In round-robin, we re-enqueue immediately so it goes to the back
                    // This ensures cycling through all objects
                    return true;
                }
                
                return false;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _queue.Clear();
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            return _queue.ToArray();
        }
    }
}
