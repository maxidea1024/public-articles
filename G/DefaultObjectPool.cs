// 일반적인 new에 비해서 매우 무겁다.
// 풀링을 할때나 효율적인거지..
// 마구 new를 하고 C# GC를 믿고 사용해야하나?

using System;
using System.Collections.Generic;

namespace Prom.Core.Util
{
    public class DefaultObjectPool<T> : IObjectPool<T> where T : class, IRecycleable<T>//, new()
    {
        private readonly Func<T> _lock = new object();
        private readonly Queue<T> _sharedQueue;
        private readonly int _sharedCapacity;

        private readonly ThreadLocal<ThreadLocalData> _threadLocalData;
        private readonly int _threadLocalCapacity;

        private readonly int _rentLimit;

        private bool _disposed;

        public T Rent()
        {
        }

        public void Return(T recycleable)
        {
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_dispose)
                    return;

                _disposed = true;

                while (_sharedQueue.Count > 0)
                {
                    _sharedQueue.Dequeue().Dispose();
                }
            }
        }
    }
}
