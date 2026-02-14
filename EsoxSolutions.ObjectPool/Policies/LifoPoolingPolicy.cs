using System.Collections.Concurrent;

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
        private readonly ConcurrentStack<T> _stack = new();

        /// <inheritdoc/>
        public string PolicyName => "LIFO";

        /// <inheritdoc/>
        public int Count => _stack.Count;

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _stack.Push(item);
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            return _stack.TryPop(out item);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _stack.Clear();
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            return _stack.ToArray();
        }
    }
}
