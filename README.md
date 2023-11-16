# EsoxSolutions.ObjectPool

## Overview

Sometimes, for reasons of efficiency, it can be quite handy to keep a pool, that is non-empty set, of initialized objects ready. This can be the case in for instance when you have database connections, which are expensive to create both in time and resources.

What you do is that make a pool of objects, usually an array, and you have one object managing that array, giving out objects, and returning, basically doing the bookkeeping.

This is a very simple implementation of such a pool, which is thread safe, and can be used in a multi-threaded environment.

**Caveat**: I consider this version to be still in beta. If you find any bugs, please report them to me, or mail me at [info@esoxsolutions.nl](info@esoxsolutions.nl)
## Usage

There are two main classes:

1. The PoolModel class which holds an object in a pool.
2. The ObjectPool which releases and returns the PoolModel objects.

### PoolModel
The PoolModel is a generic class. It takes a type parameter, which is the type of the object you want to pool. 
To get the value stored in the PoolModel, use the Unwrap method.

An example use:
```
            var initialObjects = new List<int> { 1, 2, 3 };
            var objectPool = new ObjectPool<int>(initialObjects);

            using (var model = objectPool.GetObject())
            {
                var value = model.Unwrap();
				Console.WriteLine(value);
            }
```


### ObjectPool
The objectpool as you can see administers the object. In its constructor its gets a list of pre-initialized objects. The length of this list does not change.
In case there are no more objects in the pool, an exception is raised.


### QueryableObjectPool
The QueryableObjectPool is a special type of ObjectPool. It has a Query method, which takes a predicate, and returns the first object in the pool that matches the predicate. If no object matches the predicate, an exception is raised.

An example use:
```
			var initialObjects = new List<int> { 1, 2, 3 };
			var objectPool = new QueryableObjectPool<int>(initialObjects);

			using (var model = objectPool.GetObject(x => x == 2))
			{
				var value = model.Unwrap();
				Console.WriteLine(value);
			}
```
### DynamicObjectPool
The DynamicObjectPool is a special type of object pool, which can be used to create objects on the fly. It takes a factory method, which is used to create the objects. The factory method is called when the pool is empty, and a new object is requested.
An example use:
```
			var objectPool = new DynamicObjectPool<int>(() => 1);

			using (var model = objectPool.GetObject())
			{
				var value = model.Unwrap();
				Console.WriteLine(value);
			}
```
The constructor also takes a list of pre-initialized objects. These objects are used first, before the factory method is called.

## Future work
One possible extension would be to have a timeout on the objects in the pool. If an object is not used for a certain amount of time, it is disposed. This would be useful in case you have a pool of database connections, and you want to make sure that the connections are not kept open for too long.

## Version history:
* 1.1.1: Added QueryableObjectPool
* 1.1.2: Improved threadsafety
* 1.1.3: Added DynamicObjectPool

