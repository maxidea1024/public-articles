using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace G.DataSet
{
    public interface IRank<K>
    {
        K IID();
        long Score();

        (long, K) ScoreKey();

        void Score(long s);
        void Ranking(int rank);
    }

    public class RankingList<K, T> : SortedList<(long, K), T> where K : IComparable where T : IRank<K>
    {
        private class RankComparer : Comparer<(long, K)>
        {
            public override int Compare((long, K) x, (long, K) y)
            {
                int r1 = x.Item1.CompareTo(y.Item1);
                int r2 = x.Item2.CompareTo(y.Item2);
                return r1 != 0 ? r1 : (r2 != 0 ? r2 : 0);
            }

            public int Compare(IRank<K> x, IRank<K> y) { return x.ScoreKey().CompareTo(y.ScoreKey()); }
        }

        public SemaphoreSlim _locks { get; private set; }
        public Dictionary<K, T> _table { get; private set; }

        public RankingList() : base(new RankComparer())
        {
            _locks = new SemaphoreSlim(1, 5);
            _table = new Dictionary<K, T>();
        }

        public async Task<bool> Add(T value)
        {
            await _locks.WaitAsync();
            {
                if (true != _table.TryAdd(value.IID(), value))
                {
                    _locks.Release();
                    return false;
                }
                Add(value.ScoreKey(), value);
            }
            _locks.Release();
            return true;
        }

        public async Task<bool> Remove(K key)
        {
            await _locks.WaitAsync();
            {
                if (true != _table.TryGetValue(key, out T rank))
                {
                    _locks.Release();
                    return false;
                }
                _table.Remove(key);
                Remove(rank.ScoreKey());
            }
            _locks.Release();
            return true;
        }

        public async Task<bool> Update(T value)
        {
            await _locks.WaitAsync();
            {
                if (true != _table.TryGetValue(value.IID(), out T rank))
                {
                    if (true != _table.TryAdd(value.IID(), value))
                    {
                        _locks.Release();
                        return false;
                    }
                    Add(value.ScoreKey(), value);

                    _locks.Release();
                    return true;
                }
                Remove(rank.ScoreKey());

                rank.Score(value.Score());
                Add(value.ScoreKey(), value);
            }
            _locks.Release();
            return true;
        }

        public async Task<List<T>> RankUp(bool desc = true)
        {
            var lists = new List<T>();
            int rank = 0;
            await _locks.WaitAsync();
            {
                foreach (var r in (true== desc ? Values.Reverse<T>() : Values))
                {
                    r.Ranking(++rank);
                    lists.Add(r);
                }
            }
            _locks.Release();
            return lists;
        }
    }
}
