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
    /// <typeparam name="T"></typeparam>
    public class QueryableObjectPool<T> : ObjectPool<T>
    {
        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the list of initial objects</param>
        public QueryableObjectPool(List<T> initialObjects) : base(initialObjects)
        {
        }

        /// <summary>
        /// Get objects from the pool which match the query. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">the query to be performed</param>
        /// <returns></returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            mutex.WaitOne();
            var obj = this.availableObjects.FirstOrDefault(query);
            if (obj == null)
            {
                throw new NoObjectsInPoolException("No objects matching the query available");
            }
            this.availableObjects.Remove(obj);
            this.activeObjects.Add(obj);
            mutex.ReleaseMutex();
            return new PoolModel<T>(obj, this);
        }
    }


}

