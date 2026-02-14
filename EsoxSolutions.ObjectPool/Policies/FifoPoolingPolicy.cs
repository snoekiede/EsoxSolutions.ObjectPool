using System.Collections.Concurrent;

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
        private readonly ConcurrentQueue<T> _queue = new();

        /// <inheritdoc/>
        public string PolicyName => "FIFO";

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
            return _queue.TryDequeue(out item);
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
