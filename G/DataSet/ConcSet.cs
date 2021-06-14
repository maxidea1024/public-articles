
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using G.Util;

namespace G.DataSet
{
    public class ConcSet<T> : DataSet<T>
    {
        public ConcurrentBag<T> _bag = new ConcurrentBag<T>();
        public override long Counting() { return null == _bag ? 0 : _bag.Count; }

        public override bool Add(T item)
        {
            try
            {
                _bag.Add(item);
                return true;
            }
            finally
            {
            }
        }

        public override bool Remove(T item)
        {
            try
            {
                return _bag.TryTake(out item);
            }
            finally
            {
            }
        }

        public override List<T> Lists()
        {
            try
            {   
                return _bag.ToList();
            }
            finally
            {
            }
        }

        public override List<T> Listup()
        {
            try
            {
                return new List<T>(_bag);
            }
            finally
            {
            }
        }

        public T Take(out bool res)
        {
            res = false;
            do
            {
                T key = default(T);
                try
                {
                    if (0 < _bag.Count)
                    {
                        res = _bag.TryTake(out key);
                        return key;
                    }
                }
                finally
                {

                }
                return key;

            } while (true);
        }
    }

    public class ConcListed<T> : DataListed<T>
    {
        public ConcurrentStack<T> _stack = new ConcurrentStack<T>();
        public override long Counting() { return null == _stack ? 0 : _stack.Count; }

        public override void Add(T item)
        {

            try
            {
                _stack.Push(item);
            }
            finally
            {
            }
        }

        public override bool TryPeek(out T result)
        {
            result = default(T);
            try
            {   
                if (_stack.Count > 0)                
                    return _stack.TryPeek(out result);

                return false;
            }
            finally
            {

            }
        }

        public override List<T> Lists()
        {
            try
            {
                return _stack.ToList();
            }
            finally
            {
            }
        }

        public override List<T> Listup()
        {
            try
            {
                return new List<T>(_stack);
            }
            finally
            {
            }
        }

        public override bool Take(out T result)
        {
            result = default(T);

            int tried = 0;
            do
            {
                tried++;
                if (32 < tried)
                    return false;
                try
                {
                    if (_stack.Count > 0)
                        return _stack.TryPop(out result);
                }
                finally
                {
                }

            } while (true);
        }
    }

    public class ConcQueue<T> : DataQueue<T>
    {
        private ConcurrentQueue<T> _conque = new ConcurrentQueue<T>();
        private int _maxTrial = 8;

        public override long Counting() { return null == _queue ? 0 : _conque.Count; }

        public override void Add(T item)
        {

            try
            {
                _conque.Enqueue(item);
            }
            finally
            {
            }
        }

        public override bool TryPeek(out T result)
        {
            result = default(T);
            try
            {
                if (_conque.Count > 0)
                {
                    return _conque.TryPeek(out result);
                }
                else
                {
                    return false;
                }
            }
            finally
            {

            }
        }

        public override bool Take(out T result)
        {
            result = default(T);

            int tried = 0;
            do
            {
                tried++;
                if (_maxTrial < tried)
                    return false;
                try
                {
                    if (_conque.Count > 0)
                        return _conque.TryDequeue(out result);
                }
                finally
                {
                }

            } while (true);
        }

        public override bool TryTake(out T result, int wait=0)
        {
            result = default(T);

            int tried = 0;
            do
            {
                tried++;
                if (_maxTrial < tried)
                    return false;
                try
                {
                    if (_conque.Count > 0)
                        return _conque.TryDequeue(out result);
                }
                finally
                {
                }

            } while (true);
        }
    }

    public class ConcTree<K, T> : DataTree<K, T> where T : IHash<K>
    {
        public ConcurrentDictionary<K, T> _contree = new ConcurrentDictionary<K, T>();
        public override long Counting() { return null == _contree ? 0 : _contree.Count; }

        public override bool Add(T item)
        {
            try
            {
                return _contree.TryAdd(item.IID(), item);
            }
            finally
            {
            }
        }

        public override bool Remove(K key)
        {
            try
            {
                T value = default(T);
                return _contree.TryRemove(key, out value);
            }
            finally
            {
            }
        }

        public override List<T> Lists()
        {
            try
            {
                return _contree.Values.ToList();
            }
            finally
            {
            }
        }

        public override List<T> Listup()
        {
            try
            {
                return new List<T>(_contree.Values);
            }
            finally
            {
            }
        }
    }

    public class BlockingCollectionSlim<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        public void Add(T item)
        {
            _queue.Enqueue(item);
            _autoResetEvent.Set();
        }
        public bool TryPeek(out T result)
        {
            return _queue.TryPeek(out result);
        }
        public T Take()
        {
            T item;
            while (!_queue.TryDequeue(out item))
                _autoResetEvent.WaitOne();
            return item;
        }
        public bool TryTake(out T item, TimeSpan patience)
        {
            if (_queue.TryDequeue(out item))
                return true;
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < patience)
            {
                if (_queue.TryDequeue(out item))
                    return true;
                var patienceLeft = (patience - stopwatch.Elapsed);
                if (patienceLeft <= TimeSpan.Zero)
                    break;
                else if (patienceLeft < MinWait)
                    // otherwise the while loop will degenerate into a busy loop,
                    // for the last millisecond before patience runs out
                    patienceLeft = MinWait;
                _autoResetEvent.WaitOne(patienceLeft);
            }
            return false;
        }
        private static readonly TimeSpan MinWait = TimeSpan.FromMilliseconds(1);
    }
}
