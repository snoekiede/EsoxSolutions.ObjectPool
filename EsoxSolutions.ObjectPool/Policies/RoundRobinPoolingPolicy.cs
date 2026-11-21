using EsoxSolutions.ObjectPool.Infrastructure;

namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Round-robin pooling policy that distributes usage evenly across all objects.
    /// Objects are retrieved in a circular turn-based fashion based on checkout order,
    /// ensuring even rotation even when objects are returned out of order.
    /// Best for: Load balancing, even wear distribution, connection pooling across multiple endpoints.
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public class RoundRobinPoolingPolicy<T> : IPoolingPolicy<T> where T : notnull
    {
        private readonly List<(T Item, long TurnOrder)> _available = [];
        private readonly Dictionary<T, long> _assignedTurnOrder = new(ReferenceOrValueEqualityComparer<T>.Instance);
        private readonly object _lock = new();
        private long _nextTurnSequence;

        /// <inheritdoc/>
        public string PolicyName => "RoundRobin";

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _available.Count;
                }
            }
        }

        /// <inheritdoc/>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);

            lock (_lock)
            {
                if (!_assignedTurnOrder.TryGetValue(item, out var turnOrder))
                {
                    turnOrder = ++_nextTurnSequence;
                    _assignedTurnOrder[item] = turnOrder;
                }

                // Insert keeping _available sorted by TurnOrder ascending so the next due object is at index 0
                int insertIndex = _available.Count;
                while (insertIndex > 0 && _available[insertIndex - 1].TurnOrder > turnOrder)
                {
                    insertIndex--;
                }

                _available.Insert(insertIndex, (item, turnOrder));
            }
        }

        /// <inheritdoc/>
        public bool TryTake(out T? item)
        {
            lock (_lock)
            {
                if (_available.Count == 0)
                {
                    item = default;
                    return false;
                }

                item = _available[0].Item;
                _available.RemoveAt(0);
                // Advance this object's turn order so when it is returned it rotates to the back of the cycle
                _assignedTurnOrder[item] = ++_nextTurnSequence;
                return true;
            }
        }

        /// <inheritdoc/>
        public bool TryTake(Func<T, bool> predicate, out T? item)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            lock (_lock)
            {
                for (int i = 0; i < _available.Count; i++)
                {
                    var candidate = _available[i].Item;
                    if (predicate(candidate))
                    {
                        item = candidate;
                        _available.RemoveAt(i);
                        _assignedTurnOrder[item] = ++_nextTurnSequence;
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
                _available.Clear();
                _assignedTurnOrder.Clear();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetAll()
        {
            lock (_lock)
            {
                var snapshot = new T[_available.Count];
                for (int i = 0; i < _available.Count; i++)
                {
                    snapshot[i] = _available[i].Item;
                }
                return snapshot;
            }
        }
    }
}
