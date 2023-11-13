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

## Future work
One possible extension would be to have a timeout on the objects in the pool. If an object is not used for a certain amount of time, it is disposed. This would be useful in case you have a pool of database connections, and you want to make sure that the connections are not kept open for too long.
Also it would be nice to have some sort of LINQ-query to get objects with certain values from the pool.


