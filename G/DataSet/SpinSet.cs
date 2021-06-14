using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using G.Util;

namespace G.DataSet
{
    public class SpinSet<T> : DataSet<T>
    {
        private SpinLock _locks = new SpinLock(false);
        public override long Counting() { return null == _hash ? 0 : _hash.Count; }

        public override bool Add(T item)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (true == _hash.Add(item))
                    return true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return false;
        }

        public override bool Remove(T item)
        {
            bool __locked = false;
            
            try
            {
                _locks.Enter(ref __locked);
                if (true == _hash.Remove(item))
                    return true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return false;
        }

        public override List<T> Lists()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return _hash.ToList();
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Listup()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return new List<T>(_hash);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public T Take(out bool res)
        {
            res = false;

            var spin = new SpinWait();
            do
            {
                bool __locked = false;

                T key = default(T);
                try
                {
                    _locks.Enter(ref __locked);

                    if (0 < _hash.Count)
                    {
                        var it = _hash.GetEnumerator();
                        while (it.MoveNext())
                        {
                            key = it.Current;
                            res = _hash.Remove(key);
                            return key;
                        }
                        return key;
                    }
                }
                finally
                {
                    if (__locked) _locks.Exit(false);
                }
                spin.SpinOnce();
            } while (true);
        }
    }

    public class SpinSorted<T> : DataSorted<T>
    {
        private SpinLock _locks = new SpinLock(false);
        private int _maxTrial = 8;
        public override long Counting() { return null == _sorted ? 0 : _sorted.Count; }


        public override bool Add(T item)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (true == _sorted.Add(item))
                    return true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return false;
        }

        public override bool Remove(T item)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (true == _sorted.Remove(item))
                    return true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return false;
        }

        public override List<T> Lists()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return _sorted.ToList();
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Listup()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return new List<T>(_sorted);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public T Take(out bool res, int wait = 0)
        {
            res = false;
            int tried = 0;
            var spin = new SpinWait();
            do
            {
                bool __locked = false;

                T key = default(T);
                try
                {
                    tried++;
                    if (_maxTrial < tried)
                    {
                        res = false;
                        return key;
                    }

                    _locks.Enter(ref __locked);
                    if (0 < _sorted.Count)
                    {
                        var it = _sorted.GetEnumerator();
                        while (it.MoveNext())
                        {
                            key = it.Current;
                            res = _sorted.Remove(key);
                            return key;
                        }
                        return key;
                    }
                }
                finally
                {
                    if (__locked) _locks.Exit(false);
                }
                spin.SpinOnce();
            } while (true);
        }
    }

    public class SpinQueue<T> : DataQueue<T>
    {
        private SpinLock _locks = new SpinLock(false);
        private int _maxTrial = 8;
        public override long Counting() { return null == _queue ? 0 : _queue.Count; }

        public override void Add(T item)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                _queue.Enqueue(item);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override bool TryPeek(out T data)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (_queue.Count > 0)
                {
                    data = _queue.Peek();
                    return true;
                }
                else
                {
                    data = default(T);
                    return false;
                }
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override bool Take( out T data)
        {
            data = default(T);

            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (_queue.Count > 0)
                {
                    data = _queue.Dequeue();
                    return true;
                }
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            };
            return false;
        }

        public override bool TryTake(out T data, int wait = 0)
        {
            data = default(T);
            int tried = 0;

            bool __locked = false;
            try
            {
                tried++;
                if (_maxTrial < tried)
                    return false;

                _locks.Enter(ref __locked);
                if (_queue.Count > 0)
                {
                    data = _queue.Dequeue();
                    return true;
                }
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }

            return false;
        }
    }

    public class SpinListed<T> : DataListed<T>
    {
        private SpinLock _locks = new SpinLock(false);
        private int _maxTrial = 8;

        public override long Counting() { return null == _listed ? 0 : _listed.Count; }

        public override void Add(T item)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                _listed.Add(item);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override bool Remove(T item)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (true == _listed.Remove(item))
                    return true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return false;
        }

        public override List<T> Lists()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return _listed;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Listup()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return new List<T>(_listed);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
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
                if (_maxTrial < tried)
                    return false;

                bool __locked = false;
                try
                {
                    _locks.Enter(ref __locked);

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
                    if (__locked) _locks.Exit(false);
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
                if (_maxTrial < tried)
                    return false;

                bool __locked = false;
                try
                {
                    _locks.Enter(ref __locked);
                    if (0 < _listed.Count)
                    {
                        data = _listed[0];
                        return true;
                    }
                }
                finally
                {
                    if (__locked) _locks.Exit(false);
                }
                spin.SpinOnce();
            } while (true);
        }
    }

    public class SpinRanked<K, T> : DataRanked<K,T> where T : IHash<K>
    {
        private SpinLock _locks = new SpinLock(false);
        public override long Counting() { return null == _ranked ? 0 : _ranked.Count; }

        public override bool Add(T data)
        {
            bool __success = false;
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (true == _ranked.ContainsKey(data.IID()))
                    return false;

                _ranked.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return __success;
        }

        public override bool Remove(K key)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return _ranked.Remove(key);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Lists()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);                
                return _ranked.Values.ToList();
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Listup()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return new List<T>(_ranked.Values);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }
    }

    public class SpinTree<K, T> : DataTree<K, T> where T : IHash<K>
    {
        private SpinLock _locks = new SpinLock(false);
        public override long Counting() { return null == _tree ? 0 : _tree.Count; }


        public override T Find(K key)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);

                if (true == _tree.TryGetValue(key, out T data))
                    return data;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return default(T);
        }


        public override bool Add(T data)
        {
            bool __success = false;
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                if (true == _tree.ContainsKey(data.IID()))
                    return false;

                _tree.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
            return __success;
        }

        public override bool Remove(K key)
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return _tree.Remove(key);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public bool Update(T data)
        {
            bool __locked = false;

            _locks.Enter(ref __locked);
            try
            {
                if (true == _tree.ContainsKey(data.IID()))
                    _tree.Remove(data.IID());

                _tree.Add(data.IID(), data);
                return true;
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Lists()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return _tree.Values.ToList();
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }

        public override List<T> Listup()
        {
            bool __locked = false;
            try
            {
                _locks.Enter(ref __locked);
                return new List<T>(_tree.Values);
            }
            finally
            {
                if (__locked) _locks.Exit(false);
            }
        }
    }
}