namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Priority-based pooling policy where objects are assigned priorities.
    /// Higher priority objects are retrieved first.
    /// Best for: Quality-of-service requirements, tenant-based prioritization, resource quality tiers.
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public class PriorityPoolingPolicy<T> : IPoolingPolicy<T> where T : notnull
    {
        private readonly PriorityQueue<T, int> _priorityQueue = new(Comparer<int>.Create((a, b) => b.CompareTo(a))); // Higher priority first
        private readonly Func<T, int> _prioritySelector;
        private readonly object _lock = new();
        private int _count;

        /// <summary>
        /// Initializes a new instance of the priority pooling policy
        /// </summary>
        /// <param name="prioritySelector">Function to determine the priority of an object (higher values = higher priority)</param>
        public PriorityPoolingPolicy(Func<T, int> prioritySelector)
        {
            _prioritySelector = prioritySelector ?? throw new ArgumentNullException(nameof(prioritySelector));
        }

        /// <inheritdoc/>
        public string PolicyName => "Priority";

        /// <inheritdoc/>
        public int Count => _count;

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);
            
            lock (_lock)
            {
                var priority = _prioritySelector(item);
                _priorityQueue.Enqueue(item, priority);
                _count++;
            }
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            lock (_lock)
            {
                if (_priorityQueue.TryDequeue(out item, out _))
                {
                    _count--;
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
                _priorityQueue.Clear();
                _count = 0;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            lock (_lock)
            {
                var result = new List<T>();
                var items = _priorityQueue.UnorderedItems;
                foreach (var (item, _) in items)
                {
                    result.Add(item);
                }
                return result;
            }
        }
    }
}
