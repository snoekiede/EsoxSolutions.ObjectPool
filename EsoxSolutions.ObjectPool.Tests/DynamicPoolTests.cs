﻿using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class DynamicPoolTests
    {
        [Fact]
        public void TestGetModel()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new DynamicObjectPool<Car>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            var model = objectPool.GetObject();
            var afterCount = objectPool.availableObjectCount;

            
            
            Assert.NotNull(model);
        }

        [Fact]
        public void TestAutomaticReturn()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new DynamicObjectPool<Car>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            using (var model = objectPool.GetObject())
            {
                var afterCount = objectPool.availableObjectCount;
                Assert.Equal(7, initialCount);
                Assert.Equal(6, afterCount);
            }
            var afterusingCount = objectPool.availableObjectCount;
            Assert.Equal(initialCount, afterusingCount);
        }

        [Fact]
        public void TestMultithreaded()
        {
            var initialObjects = Car.GetInitialCars();
            var objectPool = new DynamicObjectPool<Car>(initialObjects);

            var initialCount = objectPool.availableObjectCount;
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    using (var model = objectPool.GetObject())
                    {
                        var value = model.Unwrap();
                        Assert.True(!string.IsNullOrEmpty(value.Make));
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            var afterusingCount = objectPool.availableObjectCount;
            Assert.Equal(initialCount, afterusingCount);
        }
    }

    
}
