using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// A threadsafe generic object pool
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the object pool</typeparam>
    public class ObjectPool<T>
    {
        /// <summary>
        /// A list of available objects
        /// </summary>
        protected List<T> availableObjects;
        /// <summary>
        /// A list of active objects
        /// </summary>
        protected List<T> activeObjects;

        /// <summary>
        /// Simple lock object
        /// </summary>
        protected object lockObject = new();
        /// <summary>
        /// Constructor for the object pool
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects. The number of available objects does not change during the lifetime of the object-pool.</param>
        public ObjectPool(List<T> initialObjects)
        {
            this.activeObjects = new();
            this.availableObjects = initialObjects;
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            T obj;
            
            lock (lockObject)
            {
                if (this.availableObjects.Count == 0)
                {
                    throw new NoObjectsInPoolException("No objects available");
                }
                obj = this.availableObjects[0];
                this.availableObjects.RemoveAt(0);
                this.activeObjects.Add(obj);
            }
            return new PoolModel<T>(obj, this);

        }

        /// <summary>
        /// Returns an object to the pool. If the object is not in the pool, an exception is thrown.
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        /// <exception cref="NoObjectsInPoolException">Raised if the object was not in the active objects list</exception>
        public void ReturnObject(PoolModel<T> obj)
        {
            lock (lockObject)
            {
                var unwrapped = obj.Unwrap();
                if (!this.activeObjects.Contains(unwrapped))
                {
                    throw new NoObjectsInPoolException("Object not in pool");
                }
                this.activeObjects.Remove(unwrapped);
                this.availableObjects.Add(unwrapped);
            }
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int availableObjectCount => this.availableObjects.Count;
    }
}
