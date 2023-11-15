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
    /// Queryable object pool
    /// </summary>
    /// <typeparam name="T">the type to be stored in the object pool</typeparam>
    public class DynamicObjectPool<T> : ObjectPool<T> where T : class
    {
        private Func<T>? creationFunction;
        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the initial objects</param>
        public DynamicObjectPool(List<T> initialObjects) : base(initialObjects)
        {
        }

        /// <summary>
        /// Constructor with initial objects and a creation function
        /// </summary>
        /// <param name="initialObjects">The list of initial objects</param>
        /// <param name="creationFunction">The creation function</param>
        public DynamicObjectPool(List<T> initialObjects,Func<T> creationFunction) : base(initialObjects)
        {
            this.creationFunction = creationFunction;
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NoObjectsInPoolException">Thrown if no object could be found</exception>
        public override PoolModel<T> GetObject()
        {
            T obj;

            lock (lockObject)
            {
                if (this.availableObjects.Count == 0)
                {
                    if (creationFunction != null)
                    {
                        obj = creationFunction();
                        this.availableObjects.Add(obj);
                    }
                    else
                    {
                        throw new NoObjectsInPoolException("No objects available");
                    }
                }
                obj = this.availableObjects[0];
                this.availableObjects.RemoveAt(0);
                this.activeObjects.Add(obj);
            }
            return new PoolModel<T>(obj, this);
        }

        
    }
}
