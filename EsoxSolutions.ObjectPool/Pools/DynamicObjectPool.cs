using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Models;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Dynamic object pool that can create new objects using a factory method when the pool is empty
    /// </summary>
    /// <typeparam name="T">the type to be stored in the object pool</typeparam>
    public class DynamicObjectPool<T> : ObjectPool<T> where T:class
    {
        /// <summary>
        /// The factory method to be used to create new objects
        /// </summary>
        private readonly Func<T>? _factory;        

        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the initial objects</param>
        public DynamicObjectPool(List<T> initialObjects) : base(initialObjects)
        {
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        public DynamicObjectPool(Func<T> factory) : base([])
        {
            this._factory = factory;
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        /// <param name="initialObjects">list of initial objects</param>
        public DynamicObjectPool(Func<T> factory, List<T> initialObjects) : base(initialObjects)
        {
            this._factory = factory;
        }
        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NoObjectsInPoolException">Thrown if no object could be found</exception>
        public override PoolModel<T> GetObject()
        {
            T? result;

            // No lock needed, use concurrent collections
            if (this.AvailableObjects.IsEmpty)
            {
                result = this._factory?.Invoke();
                if (result == null)
                {
                    throw new NoObjectsInPoolException(PoolConstants.Messages.NoAvailableObjects);
                }
                this.AvailableObjects.Push(result);
            }
            if (!this.AvailableObjects.TryPop(out result!))
            {
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoAvailableObjects);
            }
            this.ActiveObjects.TryAdd(result, 0);
            return new PoolModel<T>(result, this);
        }
    }
}
