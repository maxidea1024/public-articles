/*
    google tcmalloc를 흉내낸 오브젝트 풀입니다.

    기존 오브젝트 풀링은 전역락과 Queue개체로 구현되어 있는데 반해,
    이 구현체는 공용 Queue와 로컬(스레드 로컬) Queue로 구현되어 있습니다.
    스레드 동기화가 필요없는 로컬 큐에 먼저 접근하고, 로컬 큐에 풀링가능한
    객체가 없으면 공유큐에 락을 걸고 빌려와서 풀링 가능한 객체를 채워주는
    형태로 처리합니다.

    기존 구현에서 ReusableTime을 사용하기에 이 구현체에도 적용해놨지만,
    ReusableTime이 도래하기 전에 할당을 요청하면, 풀링이 억제될 가능성이
    있습니다. 일정시간 동안 풀링된 객체를 반환하지 않고, 새로 생성된
    객체를 반환할 가능성이 커집니다.

    특별한 이유가 없다면, 대기시간을 지정하지 않거나 짧게 가져가는게 좋습니다.

    대부분의 코드는 .net core grpc에서 차용했습니다.


    싱글스레드에서 사용시에는 단순 풀링 정도의 효율을 보이지만,
    멀티스레드 환경에서는 적지 않은 향상이 있을 수 있습니다.


    플링되는 객체는 다음의 인터페이스를 구현해야합니다.

        public void SetReturnToPoolAction(Action<OutgoingMessage> returnToPoolAction);
        public void Return();
        public long NextReusableTime { get; set; } = 0;

*/

using System;
using System.Threading;
using System.Collections.Generic;

namespace G.Util
{
    /// <summary>
    /// Pool of objects that combines a shared pool and a thread local pool.
    /// </summary>
    public class DefaultObjectPool<T> : IObjectPool<T> where T : class, IRecycleable<T> //, new()
    {
        // Factory function.
        private readonly Func<T> _factoryFunction;

        // Queue shared between threads, access needs to be synchronized.
        private readonly object _lock = new object();
        private readonly Queue<T> _sharedQueue;
        private readonly int _sharedCapacity;

        // Thread local
        private readonly ThreadLocal<ThreadLocalData> _threadLocalData;
        private readonly int _threadLocalCapacity;

        // Instead of bringing all of them unconditionally, only about half of the number that can be accommodated locally is borrowed from the public pool.
        private readonly int _rentLimit;

        // Whether disposed or not.
        private bool _disposed;

        // Wait time for next reuse
        private long _waitRecycleTime;

        /// <summary>
        /// Initializes a new instance of <c>DefaultObjectPool</c> with given shared capacity and thread local capacity.
        /// Thread local capacity should be significantly smaller than the shared capacity as we don't guarantee immediately
        /// disposing the objects in the thread local pool after this pool is disposed (they will eventually be garbage collected
        /// after the thread that owns them has finished).
        /// On average, the shared pool will only be accessed approx. once for every <c>threadLocalCapacity / 2</c> rent or rent
        /// operations.
        /// </summary>
        /// <remarks>
        /// If waitRecycleTime is specified, if there is a busy re-request within waitRecycleTime, the possibility of not being pooled increases.
        /// </remarks>
        public DefaultObjectPool(Func<T> factoryFunction, int sharedCapacity, int threadLocalCapacity,
            TimeSpan? waitRecycleTime = null)
        {
            PreValidations.CheckArgument(sharedCapacity >= 0);
            PreValidations.CheckArgument(threadLocalCapacity >= 0);

            _waitRecycleTime = waitRecycleTime != null ? (long) waitRecycleTime.Value.TotalMilliseconds : 0;
            _factoryFunction = PreValidations.CheckNotNull(factoryFunction, nameof(factoryFunction));
            _sharedQueue = new Queue<T>(sharedCapacity);
            _sharedCapacity = sharedCapacity;
            _threadLocalData = new ThreadLocal<ThreadLocalData>(() => new ThreadLocalData(threadLocalCapacity), false);
            _threadLocalCapacity = threadLocalCapacity;
            _rentLimit = threadLocalCapacity != 1 ? threadLocalCapacity / 2 : 1; // half
        }

        /// <summary>
        /// Warm up
        /// </summary>
        public void WarmUp(int capacity)
        {
            var oldWaitRecycleTime = _waitRecycleTime;
            _waitRecycleTime = 0;

            var renteds = new List<T>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                var rented = Rent();
                renteds.Add(rented);
            }

            foreach (var rented in renteds)
            {
                rented.Return();
            }

            _waitRecycleTime = oldWaitRecycleTime;
        }

        /// <summary>
        /// Rents an item from the pool or creates a new instance if the pool is empty.
        /// Attempts to retrieve the item from the thread local pool first.
        /// If the thread local pool is empty, the item is taken from the shared pool
        /// along with more items that are moved to the thread local pool to avoid
        /// prevent acquiring the lock for shared pool too often.
        /// The methods should not be called after the pool is disposed, but it won't
        /// results in an error to do so (after depleting the items potentially left
        /// in the thread local pool, it will continue returning new objects created by the factory).
        /// </summary>
        public T Rent()
        {
            if (_waitRecycleTime <= 0)
            {
                return RentWithoutWaitingTime();
            }

            long now = SystemClock.Milliseconds;
            List<T> waitings = null;

            while (true)
            {
                var rented = RentWithoutWaitingTime();

                if (rented.NextReusableTime == 0 || now > rented.NextReusableTime)
                {
                    // Returns the pending items.
                    if (waitings != null)
                    {
                        foreach (var waiting in waitings)
                        {
                            waiting.Return();
                        }
                    }

                    return rented;
                }
                else
                {
                    // To prevent borrowing again immediately, we have to save it and return it again when it is finished.
                    // Causes unnecessary borrowing and returning.
                    waitings ??= new List<T>();
                    waitings.Add(rented);
                }
            }
        }

        private T RentWithoutWaitingTime()
        {
            var recycleable = RentInternal();
            recycleable.SetReturnToPoolAction(Return);
            return recycleable;
        }

        private T RentInternal()
        {
            var localData = _threadLocalData.Value;

            if (localData.Queue.Count > 0)
            {
                return localData.Queue.Dequeue();
            }

            if (localData.CreateBudget > 0)
            {
                localData.CreateBudget--;
                return _factoryFunction();
            }

            int itemsMoveds = 0;
            T recycled = null;
            lock (_lock)
            {
                if (_sharedQueue.Count > 0)
                {
                    recycled = _sharedQueue.Dequeue();
                }

                while (_sharedQueue.Count > 0 && itemsMoveds < _rentLimit)
                {
                    // shared -> thread local
                    localData.Queue.Enqueue(_sharedQueue.Dequeue());
                    itemsMoveds++;
                }
            }

            // If the shared pool didn't contain all _rentLimit items,
            // next time we try to rent we will just create those
            // instead of trying to grab them from the shared queue.
            // This is to guarantee we won't be accessing the shared queue too often.
            localData.CreateBudget = _rentLimit - itemsMoveds;

            return recycled ?? _factoryFunction();
        }

        /// <summary>
        /// Returns an item to the pool.
        /// Attempts to add the item to the thread local pool first.
        /// If the thread local pool is full, item is added to a shared pool,
        /// along with half of the items for the thread local pool, which
        /// should prevent acquiring the lock for shared pool too often.
        /// If called after the pool is disposed, we make best effort not to
        /// add anything to the thread local pool and we guarantee not to add
        /// anything to the shared pool (items will be disposed instead).
        /// </summary>
        public void Return(T recycleable)
        {
            PreValidations.CheckNotNull(recycleable);

            if (_waitRecycleTime > 0)
            {
                recycleable.NextReusableTime = SystemClock.Milliseconds + _waitRecycleTime;
            }

            // No global lock required yet.

            var localData = _threadLocalData.Value;
            if (localData.Queue.Count < _threadLocalCapacity && !_disposed)
            {
                localData.Queue.Enqueue(recycleable);
                return;
            }

            if (localData.DisposeBudget > 0)
            {
                localData.DisposeBudget--;
                recycleable.Dispose();
                return;
            }


            // Lock is required as it must be fetched from the shared pool.

            int itemReturneds = 0;
            int returnLimit = _rentLimit + 1;
            lock (_lock)
            {
                // 로컬큐 수용량 초과했으므로, 바로 공용큐로 반납
                if (_sharedQueue.Count < _sharedCapacity && !_disposed)
                {
                    _sharedQueue.Enqueue(recycleable);
                    itemReturneds++;
                }

                // 다른 로컬큐에서 가져갈 수 있도록 일정 수량 만큼 공용큐로 반납
                while (_sharedQueue.Count < _sharedCapacity && itemReturneds < returnLimit && !_disposed)
                {
                    // thread local -> shared
                    _sharedQueue.Enqueue(localData.Queue.Dequeue());
                    itemReturneds++;
                }
            }

            // If the shared pool could not accommodate all returnLimit items,
            // next time we try to return we will just dispose the item
            // instead of trying to return them to the shared queue.
            // This is to guarantee we won't be accessing the shared queue too often.
            localData.DisposeBudget = returnLimit - itemReturneds;

            if (itemReturneds == 0)
            {
                localData.DisposeBudget--;
                recycleable.Dispose();
            }
        }

        /// <summary>IDisposable.Dispose</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                while (_sharedQueue.Count > 0)
                {
                    _sharedQueue.Dequeue().Dispose();
                }
            }
        }


        private class ThreadLocalData
        {
            public Queue<T> Queue { get; }
            public int CreateBudget { get; set; }
            public int DisposeBudget { get; set; }

            public ThreadLocalData(int capacity)
            {
                Queue = new Queue<T>(capacity);
            }
        }
    }
}
