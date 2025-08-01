using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// A threadsafe generic object pool
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the object pool</typeparam>
    public class ObjectPool<T> : IObjectPool<T>
    {
        /// <summary>
        /// A stack of available objects for efficient O(1) operations
        /// </summary>
        protected Stack<T> availableObjects;
        /// <summary>
        /// A hash set of active objects for efficient O(1) lookups
        /// </summary>
        protected HashSet<T> activeObjects;

        /// <summary>
        /// Simple lock object
        /// </summary>
        protected object lockObject = new();

        /// <summary>
        /// Pool statistics
        /// </summary>
        protected PoolStatistics statistics = new();
        /// <summary>
        /// Constructor for the object pool
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects. The number of available objects does not change during the lifetime of the object-pool.</param>
        public ObjectPool(List<T> initialObjects)
        {
            this.activeObjects = new HashSet<T>();
            this.availableObjects = new Stack<T>(initialObjects);
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
                    statistics.PoolEmptyCount++;
                    throw new NoObjectsInPoolException("No objects available");
                }
                obj = this.availableObjects.Pop();
                this.activeObjects.Add(obj);
                
                statistics.TotalObjectsRetrieved++;
                statistics.CurrentActiveObjects = this.activeObjects.Count;
                statistics.CurrentAvailableObjects = this.availableObjects.Count;
                if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
                {
                    statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
                }
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
            lock (lockObject)
            {
                if (this.availableObjects.Count == 0)
                {
                    statistics.PoolEmptyCount++;
                    poolModel = null;
                    return false;
                }

                var obj = this.availableObjects.Pop();
                this.activeObjects.Add(obj);
                
                statistics.TotalObjectsRetrieved++;
                statistics.CurrentActiveObjects = this.activeObjects.Count;
                statistics.CurrentAvailableObjects = this.availableObjects.Count;
                if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
                {
                    statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
                }
                
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
            lock (lockObject)
            {
                var unwrapped = obj.Unwrap();
                if (!this.activeObjects.Contains(unwrapped))
                {
                    throw new NoObjectsInPoolException("Object not in pool");
                }
                this.activeObjects.Remove(unwrapped);
                this.availableObjects.Push(unwrapped);
                
                statistics.TotalObjectsReturned++;
                statistics.CurrentActiveObjects = this.activeObjects.Count;
                statistics.CurrentAvailableObjects = this.availableObjects.Count;
            }
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int availableObjectCount {
            get
            {
                int result;
                lock (lockObject)
                {
                    result = this.availableObjects.Count;
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the current pool statistics
        /// </summary>
        public PoolStatistics Statistics
        {
            get
            {
                lock (lockObject)
                {
                    statistics.CurrentActiveObjects = this.activeObjects.Count;
                    statistics.CurrentAvailableObjects = this.availableObjects.Count;
                    return statistics;
                }
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
            var timeoutMs = timeout == default ? Timeout.Infinite : (int)timeout.TotalMilliseconds;
            var deadline = timeout == default ? DateTime.MaxValue : DateTime.UtcNow.Add(timeout);

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
