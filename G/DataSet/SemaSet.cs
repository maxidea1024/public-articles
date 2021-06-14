using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using G.Util;

namespace G.DataSet
{
    public class SemaSet<T> : DataSet<T>
    {
        protected SemaphoreSlim _locks = new SemaphoreSlim(1, 1);
        public override long Counting() { return null == _hash ? 0 : _hash.Count; }

        public override bool Add(T item)
        {
            try
            {
                _locks.WaitAsync();
                return _hash.Add(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override bool Remove(T item)
        {
            try
            {
                _locks.WaitAsync();
                return _hash.Remove(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Lists()
        {
            try
            {
                _locks.WaitAsync();
                return _hash.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Listup()
        {
            try
            {
                _locks.WaitAsync();
                return new List<T>(_hash);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override async Task<bool> AddAsync(T item)
        {
            try
            {
                await _locks.WaitAsync();
                return _hash.Add(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override async Task<bool> RemoveAsync(T item)
        {
            try
            {
                await _locks.WaitAsync();
                return _hash.Remove(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override async Task<List<T>> ListsAsync()
        {
            try
            {
                await _locks.WaitAsync();
                return _hash.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }

        public override async Task<List<T>> ListupAsync()
        {
            try
            {
                await _locks.WaitAsync();
                return new List<T>(_hash);
            }
            finally
            {
                _locks.Release();
            }
        }
    }

    public class SemaSorted<T> : DataSorted<T>
    {
        protected SemaphoreSlim _locks = new SemaphoreSlim(1, 1);
        public override long Counting() { return null == _sorted ? 0 : _sorted.Count; }

        public override bool Add(T item)
        {
            try
            {
                _locks.WaitAsync();
                return _sorted.Add(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override bool Remove(T item)
        {
            try
            {
                _locks.WaitAsync();
                return _sorted.Remove(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Lists()
        {
            try
            {
                _locks.WaitAsync();
                return _sorted.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Listup()
        {
            try
            {
                _locks.WaitAsync();
                return new List<T>(_sorted);
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<bool> AddAsync(T item)
        {
            try
            {
                await _locks.WaitAsync();
                return _sorted.Add(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<bool> RemoveAsync(T item)
        {
            try
            {
                await _locks.WaitAsync();
                return _sorted.Remove(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task <List<T>> ListsAsync()
        {
            try
            {
                await _locks.WaitAsync();
                return _sorted.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<List<T>> ListupAsync()
        {
            try
            {
                await _locks.WaitAsync();
                return new List<T>(_sorted);
            }
            finally
            {
                _locks.Release();
            }
        }
    }

    public class SemaQueue<T> : DataQueue<T>
    {
        protected SemaphoreSlim _locks = new SemaphoreSlim(1, 1);
        protected int _maxTrial = 8;
        public override long Counting() { return null == _queue ? 0 : _queue.Count; }

        public override void Add(T item)
        {
            try
            {
                _locks.WaitAsync();
                _queue.Enqueue(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override bool TryPeek(out T result)
        {
            try
            {
                _locks.WaitAsync();
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
                _locks.Release();
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

                if (0 >= _queue.Count)
                {
                    spin.SpinOnce();
                    continue;
                }

                try
                {
                    _locks.WaitAsync();
                    if (0 < _queue.Count)
                    {
                        data = _queue.Dequeue();
                        return true;
                    }
                }
                finally
                {
                    _locks.Release();
                }
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

                if (0 >= _queue.Count)
                {
                    spin.SpinOnce();
                    continue;
                }

                try
                {
                    _locks.WaitAsync();
                    if (0 < _queue.Count)
                    {
                        data = _queue.Dequeue();
                        return true;
                    }
                }
                finally
                {
                    _locks.Release();
                }
            } while (true);
        }

        public async Task AddAsync(T item)
        {
            try
            {
                await _locks.WaitAsync();
                _queue.Enqueue(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<T> DequeueAsync()
        {
            try
            {
                await _locks.WaitAsync();
                return _queue.Dequeue();
            }
            finally
            {
                _locks.Release();
            }
        }
    }

    public class SemaListed<T> : DataListed<T>
    {
        protected SemaphoreSlim _locks = new SemaphoreSlim(1, 1);
        public override long Counting() { return null == _listed ? 0 : _listed.Count; }

        public override void Add(T item)
        {
            try
            {
                _locks.Wait();
                _listed.Add(item);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override bool Remove(T item)
        {
            try
            {
                _locks.Wait();
                if (true == _listed.Remove(item))
                    return true;
            }
            finally
            {
                _locks.Release();
            }
            return false;
        }

        public override List<T> Lists()
        {
            try
            {
                _locks.Wait();
                return _listed;
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Listup()
        {
            try
            {
                _locks.Wait();
                return new List<T>(_listed);
            }
            finally
            {
                _locks.Release();
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
                    _locks.Wait();

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
                    _locks.Release();
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
                    _locks.Wait();
                    if (0 < _listed.Count)
                    {
                        data = _listed[0];
                        return true;
                    }
                }
                finally
                {
                    _locks.Release();
                }
                spin.SpinOnce();
            } while (true);
        }
    }

    public class SemaRanked<K, T> : DataRanked<K, T> where T : IHash<K>
    {
        protected SemaphoreSlim _locks = new SemaphoreSlim(1, 1);
        public override long Counting() { return null == _ranked ? 0 : _ranked.Count; }

        public override bool Add(T data)
        {
            bool __success = false;
            try
            {
                _locks.Wait();
                if (true == _ranked.ContainsKey(data.IID()))
                    return false;

                _ranked.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                _locks.Release();
            }
            return __success;
        }

        public override bool Remove(K key)
        {
            try
            {
                _locks.Wait();
                return _ranked.Remove(key);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Lists()
        {
            try
            {
                _locks.Wait();
                return _ranked.Values.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Listup()
        {
            try
            {
                _locks.Wait();
                return new List<T>(_ranked.Values);
            }
            finally
            {
                _locks.Release();
            }
        }
    }

    public class SemaTree<K, T> : DataTree<K, T> where T : IHash<K>
    {
        protected SemaphoreSlim _locks = new SemaphoreSlim(1, 1);
        public override long Counting() { return null == _tree ? 0 : _tree.Count; }

        public override bool Add(T data)
        {
            bool __success = false;
            try
            {
                _locks.WaitAsync();
                if (true == _tree.ContainsKey(data.IID()))
                    return false;

                _tree.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                _locks.Release();
            }
            return __success;
        }

        public override bool Remove(K key)
        {
            try
            {
                _locks.WaitAsync();
                return _tree.Remove(key);
            }
            finally
            {
                _locks.Release();
            }
        }

        public override List<T> Listup()
        {
            try
            {
                _locks.WaitAsync();
                return _tree.Values.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<bool> AddAsync(T data)
        {
            bool __success = false;
            try
            {
                await _locks.WaitAsync();
                if (true == _tree.ContainsKey(data.IID()))
                    return false;

                _tree.Add(data.IID(), data);
                __success = true;
            }
            finally
            {
                _locks.Release();
            }
            return __success;
        }

        public async Task<bool> RemoveAsync(IHash<K> hash)
        {
            try
            {
                await _locks.WaitAsync();
                return _tree.Remove(hash.IID());
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<bool> RemoveAsync(K key)
        {
            try
            {
                await _locks.WaitAsync();
                return _tree.Remove(key);
            }
            finally
            {
                _locks.Release();
            }
        }

        public async Task<List<T>> ListupAsync()
        {
            try
            {
                await _locks.WaitAsync();
                return _tree.Values.ToList();
            }
            finally
            {
                _locks.Release();
            }
        }
    }
}
