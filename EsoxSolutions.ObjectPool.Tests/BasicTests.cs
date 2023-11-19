using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class BasicTests
    {
        [Fact]
        public void CreatePoolModel()
        {
            var pool = new ObjectPool<int>(new List<int> { 1, 2, 3 });
            var model = new PoolModel<int>(1, pool);

            Assert.NotNull(model);
            Assert.Equal(1, model.Unwrap());
        }

        [Fact]
        public void TestGetModel()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var objectPool = new ObjectPool<int>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            var model = objectPool.GetObject();
            var afterCount = objectPool.availableObjectCount;

            Assert.Equal(3, initialCount);
            Assert.Equal(2, afterCount);
            Assert.NotNull(model);
        }

        [Fact]
        public void TestAutomaticReturn()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var objectPool = new ObjectPool<int>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            using (var model = objectPool.GetObject())
            {
                var afterCount = objectPool.availableObjectCount;
                Assert.Equal(3, initialCount);
                Assert.Equal(2, afterCount);
            }
            var afterusingCount = objectPool.availableObjectCount;
            Assert.Equal(3, afterusingCount);
        }

        [Fact]
        public void TestMultithreaded()
        {
            var initialObjects = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            var objectPool = new ObjectPool<int>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    using (var model = objectPool.GetObject())
                    {
                        var value=model.Unwrap();
                        Assert.True(value > 0);
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            var afterusingCount = objectPool.availableObjectCount;
            Assert.Equal(11, afterusingCount);
        }



    }
}
