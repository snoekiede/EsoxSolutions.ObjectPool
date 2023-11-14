using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class QueryableObjectPoolTests
    {



        [Fact]
        public void TestGetModel()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var objectPool = new QueryableObjectPool<int>(initialObjects);

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
            var objectPool = new QueryableObjectPool<int>(initialObjects);

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
                        var afterCount = objectPool.availableObjectCount;
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            var afterusingCount = objectPool.availableObjectCount;
            Assert.Equal(11, afterusingCount);
        }

        [Fact]
        public void TestQuery()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new QueryableObjectPool<Car>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            var model = objectPool.GetObject(x => x.Make == "Ford");
            var afterCount = objectPool.availableObjectCount;

            Assert.Equal(7, initialCount);
            Assert.Equal(6, afterCount);
            Assert.NotNull(model);
            Assert.Equal("Ford", model.Unwrap().Make);
        }

        [Fact]
        public void TestQueryMultiThreaded()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new QueryableObjectPool<Car>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        using (var model = objectPool.GetObject(x => x.Make == "Ford"))
                        {
                            var value = model.Unwrap();
                            Assert.True(value.Make == "Ford");
                        }
                    } catch (Exception ex)
                    {
                        Assert.Equal("No objects matching the query available", ex.Message);
                    }
                    
                }));
            }
            Task.WaitAll(tasks.ToArray());
            var afterusingCount = objectPool.availableObjectCount;
            Assert.Equal(7, afterusingCount);
        }
    }

    public class Car
    {
        public string Make { get; set; }
        public string Model { get; set; }

        public Car(string Make,string Model)
        {
            this.Make = Make;
            this.Model = Model;
        }

        public static List<Car> GetInitialCars()
        {
            List<Car> result = new();

            result.Add(new Car("Ford", "Focus"));
            result.Add(new Car("Ford", "Fiesta"));
            result.Add(new Car("Ford", "Mondeo"));
            result.Add(new Car("Ford", "Mustang"));
            result.Add(new Car("Citroen", "DS"));
            result.Add(new Car("Citroen", "C1"));
            result.Add(new Car("Citroen", "C2"));

            return result;
        }
    }
}
