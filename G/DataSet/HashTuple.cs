using System;
using System.Collections.Generic;
using System.Threading;

namespace G.DataSet
{
    public class HashTuple<K1, K2, T> 
            where K1 : IComparable
            where K2 : IComparable
    {
        public sealed class TK : Tuple<K1, K2>
        {
            public TK(K1 k1, K2 k2) : base( k1, k2) { }

            public K1 k1 => Item1;
            public K2 k2 => Item2;
        }

        public Dictionary<TK, T>[] _table { get; private set; }
        public int _bucket { get; private set; }
        public object[] _locks { get; private set; }

        private int hash(TK key) { return key.GetHashCode() % _bucket; }

        private class TupleComparer : Comparer<TK>
        {
            public override int Compare( TK x, TK y)
            {
                if (x == null)
                    return 0;

                int r1 = x.Item1.CompareTo(y.Item1);
                int r2 = (y != null) ? x.Item2.CompareTo(y.Item2) : 1;
                return r1 != 0 ? r1 : (r2 != 0 ? r2 : 0);
            }

            public int Compare(K1 x, K2 y)
            {
                return x.CompareTo(y);
            }
        }

        public HashTuple()
        {
            _bucket = 37;
            _locks = new object[_bucket];
            _table = new Dictionary< TK, T>[_bucket];

            int i = 0;
            for (i = 0; i < _bucket; i++)
            {
                _locks[i] = new object();
                _table[i] = new Dictionary< TK, T>();
            }
        }

        public void Clear()
        {
            int i;
            for (i = 0; i < _bucket; i++)
            {
                Monitor.Enter(_locks[i]);
                {
                    _table[i].Clear();
                }
                Monitor.Exit(_locks[i]);
            }
        }

        public int TryAdd( K1 k1, K2 k2, T value)
        {
            var k = new TK(k1, k2);
            int h = hash(k);

            Monitor.Enter(_locks[h]);
            {
                if (true == _table[h].ContainsKey(k))
                {
                    Monitor.Exit(_locks[h]);
                    return -2;
                }

                _table[h].Add(k, value);
            }
            Monitor.Exit(_locks[h]);

            return 0;
        }

        public int Remove(K1 k1, K2 k2, T value)
        {
            var k = new TK(k1, k2);
            int h = hash(k);
            Monitor.Enter(_locks[h]);
            {
                _table[h].Remove(k);
            }
            Monitor.Exit(_locks[h]);
            return 0;
        }

        public bool TryGetValue(K1 k1, K2 k2, out T value)
        {
            value = default(T);
            var k = new TK(k1, k2);
            int h = hash(k);
            Monitor.Enter(_locks[h]);
            {
                if (true != _table[h].TryGetValue(k, out value))
                {
                    Monitor.Exit(_locks[h]);
                    return false;
                }
            }
            Monitor.Exit(_locks[h]);
            return true;
        }

        public int Count()
        {
            int cnt = 0;
            for (int i = 0; i < _bucket; i++)
            {
                Monitor.Enter(_locks[i]);
                {
                    cnt += _table[i].Count;
                }
                Monitor.Exit(_locks[i]);
            }
            return cnt;
        }

        public List<T> Listup()
        {
            var lists = new List<T>();
            for (int i = 0; i < _bucket; i++)
            {
                Monitor.Enter(_locks[i]);
                {
                    lists.AddRange(_table[i].Values);
                }
                Monitor.Exit(_locks[i]);
            }

            return lists;
        }


        public Dictionary<TK, T> EnterLock(int h)
        {
            // lock.. need
            Monitor.Enter(_locks[h]);
            return _table[h];
        }

        public void LeaveLock(int h)
        {
            // lock.. need 
            Monitor.Exit(_locks[h]);
            return;
        }
    }
}
