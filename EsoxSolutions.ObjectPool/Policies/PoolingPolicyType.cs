namespace EsoxSolutions.ObjectPool.Policies
{
    /// <summary>
    /// Enumeration of built-in pooling policy types
    /// </summary>
    public enum PoolingPolicyType
    {
        /// <summary>
        /// Last-In-First-Out: Most recently returned objects are retrieved first.
        /// Best for cache locality and temporal locality patterns.
        /// </summary>
        Lifo,

        /// <summary>
        /// First-In-First-Out: Objects are retrieved in the order they were returned.
        /// Best for fair scheduling and preventing object aging.
        /// </summary>
        Fifo,

        /// <summary>
        /// Priority-based: Higher priority objects are retrieved first.
        /// Requires a priority selector function.
        /// </summary>
        Priority,

        /// <summary>
        /// Least Recently Used: Objects not used for the longest time are retrieved first.
        /// Best for preventing staleness and ensuring all objects get used.
        /// </summary>
        LeastRecentlyUsed,

        /// <summary>
        /// Round-robin: Objects are retrieved in a circular fashion.
        /// Best for load balancing and even wear distribution.
        /// </summary>
        RoundRobin
    }
}
