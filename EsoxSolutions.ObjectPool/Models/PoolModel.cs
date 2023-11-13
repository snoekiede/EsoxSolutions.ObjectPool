using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Models
{
    public class PoolModel<T>:IDisposable
    {
        private T value;
        private ObjectPool<T> pool;

        public PoolModel(T value,ObjectPool<T> pool)
        {
            this.value = value;
            this.pool = pool;
        }

        public T Unwrap()
        {
            return this.value;
        }

        public void Dispose()
        {
            this.pool.ReturnObject(this);
        }
    }
}
