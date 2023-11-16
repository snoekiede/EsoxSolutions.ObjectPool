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
        int availableObjectCount { get; }

        /// <summary>
        /// Get an object from the pool,raises an exception if no object could be found
        /// </summary>
        /// <returns>A poolmodel</returns>
        PoolModel<T> GetObject();

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        void ReturnObject(PoolModel<T> obj);
    }
}