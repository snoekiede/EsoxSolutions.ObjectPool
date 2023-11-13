using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EsoxSolutions.ObjectPool.Pools;

namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// A wrapper for the object pool
    /// </summary>
    /// <typeparam name="T">The type of the object to be wrapped</typeparam>
    public class PoolModel<T>:IDisposable
    {
        private T value;
        private ObjectPool<T> pool;

        /// <summary>
        /// Constructor for the pool model
        /// </summary>
        /// <param name="value">The value to be wrapped</param>
        /// <param name="pool">The object pool to which this PoolModel belongs</param>
        public PoolModel(T value,ObjectPool<T> pool)
        {
            this.value = value;
            this.pool = pool;
        }

        /// <summary>
        /// Unwraps the value
        /// </summary>
        /// <returns>The value</returns>
        public T Unwrap()
        {
            return this.value;
        }

        /// <summary>
        /// Returns the poolmodel to the pool
        /// </summary>
        public void Dispose()
        {
            this.pool.ReturnObject(this);
        }
    }
}
