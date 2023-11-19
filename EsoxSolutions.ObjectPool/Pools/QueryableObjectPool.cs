using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Queryable object pool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryableObjectPool<T> : ObjectPool<T>, IQueryableObjectPool<T>
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
        /// <returns>an object from the pool</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            
            lock (lockObject)
            {

                var result = this.availableObjects.FirstOrDefault(query);
                if ((result == null) || (result.Equals(default(T))))
                {
                    throw new NoObjectsInPoolException("No objects in pool matching your query");
                }

                this.availableObjects.Remove(result);
                this.activeObjects.Add(result);
                return new PoolModel<T>(result, this);

            }

        }
    }


}

