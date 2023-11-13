using EsoxSolutions.ObjectPool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class BasicTests
    {
        [Fact]
        public void CreatePoolModel()
        {
            var pool=new ObjectPool<int>(new List<int> { 1, 2, 3 });
            var model= new PoolModel<int>(1,pool);

            Assert.NotNull(model);
            Assert.Equal(1, model.Unwrap());
        }

        [Fact]
        public void TestGetModel()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var objectPool = new ObjectPool<int>(initialObjects);
            
            var initialCount = objectPool.GetAvailableObjectsCount();
            var model = objectPool.GetObject();
            var afterCount = objectPool.GetAvailableObjectsCount();

            Assert.Equal(3, initialCount);
            Assert.Equal(2, afterCount);
            Assert.NotNull(model);
        }

        [Fact]
        public void TestAutomaticReturn()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var objectPool = new ObjectPool<int>(initialObjects);

            var initialCount = objectPool.GetAvailableObjectsCount();
            using (var model = objectPool.GetObject())
            {
                var afterCount = objectPool.GetAvailableObjectsCount();
                Assert.Equal(3, initialCount);
                Assert.Equal(2, afterCount);
            }
            var afterusingCount = objectPool.GetAvailableObjectsCount();
            Assert.Equal(3, afterusingCount);
        }



    }
}
