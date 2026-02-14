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
