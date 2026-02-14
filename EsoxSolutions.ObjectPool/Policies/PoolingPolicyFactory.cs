namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Factory for creating pooling policy instances
    /// </summary>
    public static class PoolingPolicyFactory
    {
        /// <summary>
        /// Creates a pooling policy based on the specified type
        /// </summary>
        /// <typeparam name="T">The type of object managed by the pool</typeparam>
        /// <param name="policyType">The type of policy to create</param>
        /// <param name="prioritySelector">Optional priority selector function (required for Priority policy)</param>
        /// <returns>A new pooling policy instance</returns>
        /// <exception cref="ArgumentException">Thrown when Priority type is specified without a priority selector</exception>
        public static IPoolingPolicy<T> Create<T>(
            PoolingPolicyType policyType,
            Func<T, int>? prioritySelector = null) where T : notnull
        {
            return policyType switch
            {
                PoolingPolicyType.Lifo => new LifoPoolingPolicy<T>(),
                PoolingPolicyType.Fifo => new FifoPoolingPolicy<T>(),
                PoolingPolicyType.LeastRecentlyUsed => new LeastRecentlyUsedPolicy<T>(),
                PoolingPolicyType.RoundRobin => new RoundRobinPoolingPolicy<T>(),
                PoolingPolicyType.Priority => prioritySelector != null
                    ? new PriorityPoolingPolicy<T>(prioritySelector)
                    : throw new ArgumentException("Priority selector is required for Priority policy type", nameof(prioritySelector)),
                _ => throw new ArgumentOutOfRangeException(nameof(policyType), policyType, "Unknown pooling policy type")
            };
        }

        /// <summary>
        /// Creates a LIFO (Last-In-First-Out) pooling policy
        /// </summary>
        /// <typeparam name="T">The type of object managed by the pool</typeparam>
        /// <returns>A LIFO pooling policy</returns>
        public static IPoolingPolicy<T> CreateLifo<T>() where T : notnull
            => new LifoPoolingPolicy<T>();

        /// <summary>
        /// Creates a FIFO (First-In-First-Out) pooling policy
        /// </summary>
        /// <typeparam name="T">The type of object managed by the pool</typeparam>
        /// <returns>A FIFO pooling policy</returns>
        public static IPoolingPolicy<T> CreateFifo<T>() where T : notnull
            => new FifoPoolingPolicy<T>();

        /// <summary>
        /// Creates a Priority-based pooling policy
        /// </summary>
        /// <typeparam name="T">The type of object managed by the pool</typeparam>
        /// <param name="prioritySelector">Function to determine object priority</param>
        /// <returns>A Priority pooling policy</returns>
        public static IPoolingPolicy<T> CreatePriority<T>(Func<T, int> prioritySelector) where T : notnull
            => new PriorityPoolingPolicy<T>(prioritySelector);

        /// <summary>
        /// Creates a Least Recently Used (LRU) pooling policy
        /// </summary>
        /// <typeparam name="T">The type of object managed by the pool</typeparam>
        /// <returns>An LRU pooling policy</returns>
        public static IPoolingPolicy<T> CreateLeastRecentlyUsed<T>() where T : notnull
            => new LeastRecentlyUsedPolicy<T>();

        /// <summary>
        /// Creates a Round-robin pooling policy
        /// </summary>
        /// <typeparam name="T">The type of object managed by the pool</typeparam>
        /// <returns>A Round-robin pooling policy</returns>
        public static IPoolingPolicy<T> CreateRoundRobin<T>() where T : notnull
            => new RoundRobinPoolingPolicy<T>();
    }
}
