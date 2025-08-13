using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class DynamicPoolTests
    {
        [Fact]
        public void TestGetModel()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new DynamicObjectPool<Car>(initialObjects);
            var model = objectPool.GetObject();
            Assert.NotNull(model);
        }

        [Fact]
        public void TestAutomaticReturn()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new DynamicObjectPool<Car>(initialObjects);

            var initialCount = objectPool.AvailableObjectCount;
            using (objectPool.GetObject())
            {
                var afterCount = objectPool.AvailableObjectCount;
                Assert.Equal(7, initialCount);
                Assert.Equal(6, afterCount);
            }
            var afterusingCount = objectPool.AvailableObjectCount;
            Assert.Equal(initialCount, afterusingCount);
        }

        [Fact]
        public void TestMultithreaded()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new DynamicObjectPool<Car>(initialObjects);

            var initialCount = objectPool.AvailableObjectCount;
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    using var model = objectPool.GetObject();
                    var value = model.Unwrap();
                    Assert.True(!string.IsNullOrEmpty(value.Make));
                }));
            }
            Task.WaitAll(tasks.ToArray());
            var afterusingCount = objectPool.AvailableObjectCount;
            Assert.Equal(initialCount, afterusingCount);
        }

        [Fact]
        public void TestObjectCreation()
        {
            var initialObject = Car.GetInitialCars().Take(2).ToList();
            var objectPool = new DynamicObjectPool<Car>(() => new Car("Ford", "NewCreated"), initialObject);
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    using var model = objectPool.GetObject();
                    Thread.Sleep(100);
                    var value = model.Unwrap();
                    Assert.True(!string.IsNullOrEmpty(value.Make));
                }));
            }
            Task.WaitAll(tasks.ToArray());
            
        }
    }

    
}
