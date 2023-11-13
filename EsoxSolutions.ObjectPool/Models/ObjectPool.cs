using EsoxSolutions.ObjectPool.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// A threadsafe generic object pool
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the object pool</typeparam>
    public class ObjectPool<T>
    {
        private List<T> availableObjects;
        private List<T> activeObjects;
        private object lockObject = new object();
        public ObjectPool(List<T> initialObjects)
        {
            this.activeObjects = new();
            this.availableObjects = initialObjects;
        }

        public PoolModel<T> GetObject()
        {
            lock (lockObject)
            {
                if (this.availableObjects.Count == 0)
                {
                    throw new NoObjectsInPoolException("No objects available");
                }
                var obj = this.availableObjects[0];
                this.availableObjects.RemoveAt(0);
                this.activeObjects.Add(obj);
                return new PoolModel<T>(obj, this);
            }
        }

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

        public int availableObjectCount => this.availableObjects.Count;
    }
}
