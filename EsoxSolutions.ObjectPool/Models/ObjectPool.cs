using EsoxSolutions.ObjectPool.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Models
{
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
                    throw new ArgumentException("Object not in pool");
                }
                this.activeObjects.Remove(unwrapped);
                this.availableObjects.Add(unwrapped);
            }
        }

        public int GetAvailableObjectsCount()
        {
            return this.availableObjects.Count;
        }
    }
}
