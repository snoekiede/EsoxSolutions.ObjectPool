using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Dynamic object pool that can create new objects using a factory method when the pool is empty
    /// </summary>
    /// <typeparam name="T">the type to be stored in the object pool</typeparam>
    public class DynamicObjectPool<T> : ObjectPool<T>,IObjectPool<T> where T:class
    {
        /// <summary>
        /// The factory method to be used to create new objects
        /// </summary>
        private readonly Func<T>? _factory = null;        

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
            T? obj;

            lock (LockObject)
            {
                if (this.AvailableObjects.Count == 0)
                {
                    obj = this._factory?.Invoke();
                    if (obj == null)
                    {
                        throw new NoObjectsInPoolException("No objects available");
                    }
                    this.AvailableObjects.Push(obj);
                }
                obj = this.AvailableObjects.Pop();
                this.ActiveObjects.Add(obj);
            }
            return new PoolModel<T>(obj, this);
        }

        
    }
}
