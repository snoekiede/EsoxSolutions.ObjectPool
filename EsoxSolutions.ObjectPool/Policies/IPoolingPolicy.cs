namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Defines the strategy for how objects are stored and retrieved from the pool
    /// </summary>
    /// <typeparam name="T">The type of object managed by the pool</typeparam>
    public interface IPoolingPolicy<T> where T : notnull
    {
        /// <summary>
        /// Adds an object to the pool according to the policy
        /// </summary>
        /// <param name="item">The object to add</param>
        void Add(T item);

        /// <summary>
        /// Attempts to retrieve an object from the pool according to the policy
        /// </summary>
        /// <param name="item">The retrieved object, if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        bool TryTake(out T? item);

        /// <summary>
        /// Attempts to retrieve an object matching the specified predicate from the pool
        /// according to the policy ordering, without exposing intermediate state to other threads.
        /// </summary>
        /// <param name="predicate">The predicate used to select an object</param>
        /// <param name="item">The retrieved object, if successful</param>
        /// <returns>True if a matching object was found and removed, false otherwise</returns>
        bool TryTake(Func<T, bool> predicate, out T? item)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            var buffer = new List<T>();
            bool found = false;
            item = default;

            while (TryTake(out var candidate))
            {
                if (!found && candidate is not null && predicate(candidate))
                {
                    item = candidate;
                    found = true;
                    break;
                }

                if (candidate is not null)
                {
                    buffer.Add(candidate);
                }
            }

            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                Add(buffer[i]);
            }

            return found;
        }

        /// <summary>
        /// Gets the current count of objects in the pool
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Clears all objects from the pool
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets all objects currently in the pool (for diagnostics/inspection)
        /// </summary>
        /// <returns>Enumerable of all pooled objects</returns>
        IEnumerable<T> GetAll();

        /// <summary>
        /// Gets the name/type of the policy for diagnostics
        /// </summary>
        string PolicyName { get; }
    }
}
