# Changelog

All notable changes to **EsoxSolutions.ObjectPool** are documented here.

This project adheres to [Semantic Versioning](https://semver.org/) and
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) conventions.

---

## [Unreleased]

---

## [4.2.0] — 2025-07-xx — Production Hardening

### Fixed
- **`DynamicObjectPool.ReturnObject` validation bypass** — the `if (!valid)` guard block
  and the `ExecuteOnValidationFailed` lifecycle hook call were silently dropped during
  the 4.1.0 refactor. Invalid objects were unconditionally returned to the pool.
  Guard restored; invalid objects are now discarded and the hook is invoked.
- **`DynamicObjectPool.DisposeAsync` resource leak** — the eviction timer, circuit
  breaker, and eviction manager were not disposed when `DisposeAsync` was called.
  An `override DisposeAsync` now disposes these components before delegating to the
  base class.
- **`SemaphoreSlim` not disposed** — the availability-signal semaphore was allocated in
  `ObjectPool<T>` and `QueryableObjectPool<T>` but never disposed. Both `Dispose(bool)`
  and `DisposeAsync` paths now dispose the semaphore.
- **TOCTOU race in object return** — `ContainsKey(key)` followed by `TryRemove(key)` left
  a window where another thread could remove the entry. All sites replaced with a
  single atomic `TryRemove` call.
- **`OutOfMemoryException` swallowed** — nineteen `catch (Exception ex)` blocks across
  nine files (lifecycle hooks, circuit breaker, validation, eviction, warm-up, scoped
  pool cleanup) now carry `when (ex is not OutOfMemoryException)` guards so
  `OutOfMemoryException` always propagates to the caller.

### Changed
- **`PoolStatistics` — atomic counters** — replaced non-atomic `++` / `--` increments
  with `Interlocked.Increment`, `Interlocked.Read`, and a CAS loop for the
  `PeakActiveObjects` high-watermark. Added `IncrementRetrieved`, `IncrementReturned`,
  `IncrementPoolEmpty`, and `UpdatePeakIfHigher` helpers; exposed a
  `Reset(int currentActive, int currentAvailable)` method for snapshot-accurate resets.
- **`Disposed` flags made `volatile`** — `Disposed` fields in `ObjectPool<T>`,
  `QueryableObjectPool<T>`, `DynamicObjectPool<T>`, `CircuitBreaker`, and
  `EvictionManager<T>` are now `volatile`, ensuring cross-thread visibility without
  acquiring a lock.
- **`PoolModel<T>` double-dispose guard** — uses `Interlocked.CompareExchange` on a
  dedicated `_returnGuard` field so exactly one thread returns the object to the pool;
  `_disposed` is set with `Volatile.Write` only after the return completes, preventing
  a premature `ObjectDisposedException` from `Unwrap()`.
- **`GetObjectAsync` — signal-driven** — replaced `Task.Delay` busy-wait polling with
  `SemaphoreSlim.WaitAsync`. Callers are woken immediately when a slot becomes
  available; the semaphore is released on every successful `ReturnObject` /
  `ReturnObjectAsync`. Token cancellation now throws `OperationCanceledException`
  (via `ThrowIfCancellationRequested()`) rather than `TaskCanceledException`.
- **`ConfigureAwait(false)` throughout** — all `await` expressions in library code now
  carry `ConfigureAwait(false)`, preventing unnecessary sync-context captures and
  potential deadlocks in frameworks with a custom `SynchronizationContext`.
- **`LeastRecentlyUsedPolicy<T>.TryTake` — O(n) single pass** — replaced
  `OrderBy().First()` (O(n log n), LINQ allocations) with a manual `foreach` tracking
  the minimum `LastAccessed` timestamp — zero extra allocations.

### Quality
- 235 tests passing across .NET 8, 9, and 10 — no regressions introduced.

---

## [4.1.0] — 2025 — Pooling Policies & Async Disposal

### Added
- **Pooling policies** — configurable retrieval strategies: LIFO (default), FIFO,
  Priority, LRU, and Round-Robin.
- **`IAsyncDisposable` support** — pool and `PoolModel<T>` implement `IAsyncDisposable`
  for proper async cleanup of resources such as database connections and gRPC channels.
- **Async validation** — `WithAsyncValidation(Func<T, Task<bool>>)` builder option
  validates objects asynchronously when they are returned to the pool.
- **Enhanced AOT support** — Native AOT and trimming compatibility improvements.
- **Package validation** — NuGet `EnablePackageValidation` enabled.
- **SourceLink** — step-through debugging from NuGet package enabled.

---

## [4.0.0] — 2025 — Complete Production-Ready Suite

### Added
- **Dependency injection** — first-class ASP.NET Core and Generic Host support via
  `AddObjectPool<T>()`, `AddObjectPools()`, fluent builder API.
- **ASP.NET Core Health Checks** — `AddObjectPoolHealthChecks()` with configurable
  utilisation thresholds; Kubernetes liveness/readiness probe-compatible.
- **OpenTelemetry metrics** — native `System.Diagnostics.Metrics` instrument
  (`Meter`, `Counter`, `Gauge`) for pool utilisation, retrieve/return counts, and
  pool-empty events; Prometheus-format exporter included.
- **Pool warm-up** — `IObjectPoolWarmer<T>` and `PoolWarmupHostedService<T>` for
  pre-population at start-up, eliminating cold-start latency.
- **Eviction / TTL** — `EvictionManager<T>` with configurable time-to-live and idle
  timeout for automatic removal of stale objects.
- **Circuit breaker** — `CircuitBreaker` protects acquisition/factory paths from
  cascading failures; configurable failure threshold and recovery window.
- **Lifecycle hooks** — `LifecycleHookManager<T>` executes custom delegates at object
  create, acquire, return, evict, and dispose events.
- **Scoped pools** — `ScopedPoolManager<T>` provides per-tenant / per-context pool
  isolation with automatic scope cleanup.
- **Multiple pool registrations** — `AddObjectPools()` registers named pools from
  configuration.

---

## [3.0.0] — 2025 — Performance & Reliability

### Fixed
- **Race condition in `DynamicObjectPool`** — object-creation failures under high
  concurrency caused by a TOCTOU gap; resolved with lock-protected factory invocation.
- **Thread-safe disposal in `PoolModel<T>`** — disposal pattern hardened with modern
  atomic operations.

### Changed
- **`TryGetObject(query)` — 20-40% faster** — early-exit on first match; redundant
  snapshots and LINQ overhead removed.

### Added
- **C# 14 modernisation** — collection expressions, primary constructors,
  `ArgumentNullException.ThrowIfNull`, sealed classes.

### Quality
- 186 tests passing; stress-tested with 500 concurrent threads on 100 objects.

---

## [2.x] and earlier

Legacy releases. Refer to Git history for details.

---

[Unreleased]: https://github.com/snoekiede/EsoxSolutions.ObjectPool/compare/v4.2.0...HEAD
[4.2.0]: https://github.com/snoekiede/EsoxSolutions.ObjectPool/compare/v4.1.0...v4.2.0
[4.1.0]: https://github.com/snoekiede/EsoxSolutions.ObjectPool/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/snoekiede/EsoxSolutions.ObjectPool/compare/v3.0.0...v4.0.0
[3.0.0]: https://github.com/snoekiede/EsoxSolutions.ObjectPool/compare/v2.0.0...v3.0.0
