using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using G.Util;

namespace G.DataSet
{
    public class LockSet<T> : DataSet<T>
    {
        protected readonly object _locks = new object();
        public override long Counting() { return null == _hash ? 0 : _hash.Count; }

        public override bool Add(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                if (true == _hash.Add(item))
                    return true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return false;
        }

        public override bool Remove(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                if (true == _hash.Remove(item))
                    return true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return false;
        }

        public override List<T> Lists()
        {
            try
            {
                Monitor.Enter(_locks);
                return _hash.ToList();
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Listup()
        {
            try
            {
                Monitor.Enter(_locks);
                return new List<T>(_hash);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }
    }

    public class LockSorted<T> : DataSorted<T>
    {
        protected readonly object _locks = new object();
        public override long Counting() { return null == _sorted ? 0 : _sorted.Count; }

        public override bool Add(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                if (true == _sorted.Add(item))
                    return true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return false;
        }

        public override bool Remove(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                if (true == _sorted.Remove(item))
                    return true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return false;
        }

        public override List<T> Lists()
        {
            try
            {
                Monitor.Enter(_locks);
                return _sorted.ToList();
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Listup()
        {
            try
            {
                Monitor.Enter(_locks);
                return new List<T>(_sorted);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }
    }

    public class LockQueue<T> : DataQueue<T>
    {
        protected readonly object _locks = new object();
        protected readonly int _maxTrial = 8;
        public override long Counting() { return null == _queue ? 0 : _queue.Count; }

        public override void Add(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                _queue.Enqueue(item);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override bool TryPeek(out T result)
        {
            try
            {
                Monitor.Enter(_locks);
                if (_queue.Count > 0)
                {
                    result = _queue.Peek();
                    return true;
                }
                else
                {
                    result = default(T);
                    return false;
                }
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override bool Take(out T data)
        {
            data = default(T);
            int tried = 0;
            var spin = new SpinWait();
            do
            {
                tried++;
                if (_maxTrial < tried)
                    return false;
                try
                {
                    Monitor.Enter(_locks);
                    if (_queue.Count > 0)
                    {
                        data = _queue.Dequeue();
                        return true;
                    }
                }
                finally
                {
                    Monitor.Exit(_locks);
                }
                spin.SpinOnce();
            } while (true);
        }

        public override bool TryTake(out T data, int wait = 0)
        {
            data = default(T);
            int tried = 0;
            var spin = new SpinWait();
            do
            {
                tried++;
                if (_maxTrial < tried)
                    return false;
                try
                {
                    Monitor.Enter(_locks);
                    if (_queue.Count > 0)
                    {
                        data = _queue.Dequeue();
                        return true;
                    }
                }
                finally
                {
                    Monitor.Exit(_locks);
                }
                spin.SpinOnce();
            } while (true);
        }
    }

    public class LockListed<T> : DataListed<T>
    {
        protected readonly object _locks = new object();
        public override long Counting() { return null == _listed ? 0 : _listed.Count; }

        public override void Add(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                _listed.Add(item);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override bool Remove(T item)
        {
            try
            {
                Monitor.Enter(_locks);
                if (true == _listed.Remove(item))
                    return true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return false;
        }

        public override List<T> Lists()
        {
            try
            {
                Monitor.Enter(_locks);
                return _listed;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Listup()
        {
            try
            {
                Monitor.Enter(_locks);
                return new List<T>(_listed);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override bool Take(out T data)
        {
            data = default(T);
            int tried = 0, ix = 0;

            var spin = new SpinWait();
            do
            {
                tried++;
                if (32 < tried)
                    return false;
                try
                {
                    Monitor.Enter(_locks);

                    if (0 < _listed.Count)
                    {
                        ix = _listed.Count - 1;
                        data = _listed[ix];
                        _listed.RemoveAt(ix);
                        return true;
                    }
                }
                finally
                {
                    Monitor.Exit(_locks);
                }
                spin.SpinOnce();
            } while (true);
        }

        public override bool TryPeek(out T data)
        {
            data = default(T);
            int tried = 0;

            var spin = new SpinWait();
            do
            {
                tried++;
                if (32 < tried)
                    return false;
                try
                {
                    Monitor.Enter(_locks);
                    if (0 < _listed.Count)
                    {
                        data = _listed[0];
                        return true;
                    }
                }
                finally
                {
                    Monitor.Exit(_locks);
                }
                spin.SpinOnce();
            } while (true);
        }
    }

    public class LockRanked<K, T> : DataRanked<K, T> where T : IHash<K>
    {
        protected readonly object _locks = new object();
        public override long Counting() { return null == _ranked ? 0 : _ranked.Count; }

        public override bool Add(T data)
        {
            bool __success = false;
            try
            {
                Monitor.Enter(_locks);
                if (true == _ranked.ContainsKey(data.IID()))
                    return false;

                _ranked.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return __success;
        }

        public override bool Remove(K key)
        {
            try
            {
                Monitor.Enter(_locks);
                return _ranked.Remove(key);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Lists()
        {
            try
            {
                Monitor.Enter(_locks);
                return _ranked.Values.ToList();
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Listup()
        {
            try
            {
                Monitor.Enter(_locks);
                return new List<T>(_ranked.Values);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }
    }

    public class LockTree<K, T> : DataTree<K, T> where T : class, IHash<K>
    {
        protected readonly object _locks = new object();
        public override long Counting() { return null == _tree ? 0 : _tree.Count; }

        public override T Find(K key)
        {
            try
            {
                Monitor.Enter(_locks);
                if (true == _tree.TryGetValue(key, out T data))
                    return data;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return null;
        }

        public override bool Add(T data)
        {
            bool __success = false;
            try
            {
                Monitor.Enter(_locks);
                if (true == _tree.ContainsKey(data.IID()))
                    return false;

                _tree.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return __success;
        }

        public override bool Remove(K key)
        {
            try
            {
                Monitor.Enter(_locks);
                return _tree.Remove(key);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Lists()
        {
            try
            {
                Monitor.Enter(_locks);
                return _tree.Values.ToList();
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public override List<T> Listup()
        {
            try
            {
                Monitor.Enter(_locks);
                return new List<T>(_tree.Values);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }
    }

    public class LockTable<K, T> : Dictionary<K, T>
    {
        protected readonly object _locks = new object();

        public int tryCount()
        {
            try
            {
                Monitor.Enter(_locks);
                return Count;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public bool tryFind(K key, out T data)
        {
            try
            {
                Monitor.Enter(_locks);
                return TryGetValue(key, out data);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public bool tryUpdate(K key, T data)
        {
            try
            {
                Monitor.Enter(_locks);
                if (false == ContainsKey(key))
                { 
                    Add(key, data);
                    return true;
                }

                this[key] = data;
                return true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public bool tryAdd(K key, T data)
        {
            bool __success = false;
            try
            {
                Monitor.Enter(_locks);
                if (true == ContainsKey(key))
                    return false;

                Add(key, data);
                __success = true;
            }
            finally
            {
                Monitor.Exit(_locks);
            }
            return __success;
        }

        public bool tryRemove(K key)
        {
            try
            {
                Monitor.Enter(_locks);
                return Remove(key);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public List<T> Lists()
        {
            try
            {
                Monitor.Enter(_locks);
                return Values.ToList();
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }

        public List<T> Listup()
        {
            try
            {
                Monitor.Enter(_locks);
                return new List<T>(Values);
            }
            finally
            {
                Monitor.Exit(_locks);
            }
        }
    }
}
