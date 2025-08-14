using EsoxSolutions.ObjectPool.Models;

namespace EsoxSolutions.ObjectPool.Interfaces
{
    /// <summary>
    /// Interface for object pools
    /// </summary>
    /// <typeparam name="T">The object to be stored in the pool</typeparam>
    public interface IObjectPool<T>
    {
        /// <summary>
        /// Get the number of available objects
        /// </summary>
        int AvailableObjectCount { get; }

        /// <summary>
        /// Get an object from the pool,raises an exception if no object could be found
        /// </summary>
        /// <returns>A poolmodel</returns>
        PoolModel<T> GetObject();

        /// <summary>
        /// Try to get an object from the pool without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        bool TryGetObject(out PoolModel<T>? poolModel);

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        void ReturnObject(PoolModel<T> obj);

        /// <summary>
        /// Asynchronously get an object from the pool
        /// </summary>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        Task<PoolModel<T>> GetObjectAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default);
    }
}