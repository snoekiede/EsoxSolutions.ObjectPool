using EsoxSolutions.ObjectPool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Interfaces
{
    /// <summary>
    /// Queryable object pool
    /// </summary>
    /// <typeparam name="T">The object to be stored in the pool</typeparam>
    public interface IQueryableObjectPool<T>:IObjectPool<T>
    {
        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <returns>an object from the pool</returns>
        public PoolModel<T> GetObject(Func<T, bool> query);
    }
}
