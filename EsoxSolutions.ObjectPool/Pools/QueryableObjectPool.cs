using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Queryable object pool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryableObjectPool<T> : IQueryableObjectPool<T>
    {
        /// <summary>
        /// A list of available objects for queryable operations
        /// </summary>
        protected List<T> AvailableObjects;
        /// <summary>
        /// A hash set of active objects for efficient O(1) lookups
        /// </summary>
        protected HashSet<T> ActiveObjects;
        /// <summary>
        /// Simple lock object
        /// </summary>
        protected object LockObject = new();

        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the list of initial objects</param>
        public QueryableObjectPool(List<T> initialObjects)
        {
            this.ActiveObjects = [];
            this.AvailableObjects = new List<T>(initialObjects);
        }



        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int AvailableObjectCount
        {
            get
            {
                int result;
                lock (LockObject)
                {
                    result = this.AvailableObjects.Count;
                }
                return result;
            }
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            T obj;

            lock (LockObject)
            {
                if (this.AvailableObjects.Count == 0)
                {
                    throw new NoObjectsInPoolException("No objects available");
                }
                obj = this.AvailableObjects[^1];
                this.AvailableObjects.RemoveAt(this.AvailableObjects.Count - 1);
                this.ActiveObjects.Add(obj);
            }
            return new PoolModel<T>(obj, this);
        }

        /// <summary>
        /// Try to get an object from the pool without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        public bool TryGetObject(out PoolModel<T>? poolModel)
        {
            lock (LockObject)
            {
                if (this.AvailableObjects.Count == 0)
                {
                    poolModel = null;
                    return false;
                }

                var obj = this.AvailableObjects[^1];
                this.AvailableObjects.RemoveAt(this.AvailableObjects.Count - 1);
                this.ActiveObjects.Add(obj);
                poolModel = new PoolModel<T>(obj, this);
                return true;
            }
        }

        /// <summary>
        /// Returns an object to the pool. If the object is not in the pool, an exception is thrown.
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        /// <exception cref="NoObjectsInPoolException">Raised if the object was not in the active objects list</exception>
        public void ReturnObject(PoolModel<T> obj)
        {
            lock (LockObject)
            {
                var unwrapped = obj.Unwrap();
                if (!this.ActiveObjects.Contains(unwrapped))
                {
                    throw new NoObjectsInPoolException("Object not in pool");
                }
                this.ActiveObjects.Remove(unwrapped);
                this.AvailableObjects.Add(unwrapped);
            }
        }

        /// <summary>
        /// Get objects from the pool which match the query. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">the query to be performed</param>
        /// <returns>an object from the pool</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            lock (LockObject)
            {
                var result = this.AvailableObjects.FirstOrDefault(query);
                if ((result == null) || (result.Equals(default(T))))
                {
                    throw new NoObjectsInPoolException("No objects in pool matching your query");
                }

                this.AvailableObjects.Remove(result);
                this.ActiveObjects.Add(result);
                return new PoolModel<T>(result, this);
            }
        }

        /// <summary>
        /// Try to get an object from the pool that matches the query without throwing an exception
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if a matching object was retrieved, false otherwise</returns>
        public bool TryGetObject(Func<T, bool> query, out PoolModel<T>? poolModel)
        {
            lock (LockObject)
            {
                var result = this.AvailableObjects.FirstOrDefault(query);
                if ((result == null) || (result.Equals(default(T))))
                {
                    poolModel = null;
                    return false;
                }

                this.AvailableObjects.Remove(result);
                this.ActiveObjects.Add(result);
                poolModel = new PoolModel<T>(result, this);
                return true;
            }
        }

        /// <summary>
        /// Asynchronously get an object from the pool
        /// </summary>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public async Task<PoolModel<T>> GetObjectAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            var deadline = timeout == TimeSpan.Zero ? DateTime.MaxValue : DateTime.UtcNow.Add(timeout);

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (TryGetObject(out var poolModel))
                {
                    return poolModel!;
                }

                // Wait a short time before trying again
                await Task.Delay(10, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new TimeoutException("Timeout waiting for object from pool");
        }
    }
}

