using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace G.DataSet
{
    public class HashLocks<K, T>
        where T : IHash<K>
    {
        public long _count;
        public long _bucket { get; private set; }
        public int _timeout { get; private set; }

        private string __message = string.Empty;
        private string __caller = string.Empty;
        private int __line = 0;

        public SemaphoreSlim[] _locks { get; private set; }
        public Dictionary<K, T>[] _table { get; private set; }

        public (SemaphoreSlim _lock, Dictionary<K, T> _tbl) SemaphoreTable(int h)
        {
            if (0 > h || h >= _bucket)
                return (null, null);

            return (_locks[h], _table[h]);
        }

        public HashLocks()
        {
            _count = 0;
            _bucket = 37;
            _timeout = 3000;
            _locks = new SemaphoreSlim[_bucket];
            _table = new Dictionary<K, T>[_bucket];

            int i;
            for (i = 0; i < _bucket; i++)
            {
                _locks[i] = new SemaphoreSlim(1, 5);
                _table[i] = new Dictionary<K, T>();
            }
        }

        public HashLocks(int slot, int timeout)
        {
            _count = 0;
            _bucket = slot;

            _timeout = timeout;
            if (1000 > timeout)
                _timeout = 1000;
            if (30000 < timeout)
                _timeout = 30000;

            _locks = new SemaphoreSlim[_bucket];
            _table = new Dictionary<K, T>[_bucket];

            int i;
            for (i = 0; i < _bucket; i++)
            {
                _locks[i] = new SemaphoreSlim(1, 1);
                _table[i] = new Dictionary<K, T>();
            }
        }

        public async Task Clear(
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            int i;
            for (i = 0; i < _bucket; i++)
            {
                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Clear, {3}:{4}", __message, __caller, __line, _caller, _line));

                _table[i].Clear();
                _locks[i].Release();
            }
            _count = 0;

            __message = "HashLocks_Clear";
            __caller = _caller;
            __line = _line;
        }

        public async Task<bool> Find(IHash<K> key,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;
            h = key.Hash() % _bucket;

            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Find, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (true != _table[h].ContainsKey(key.IID()))
            {
                _locks[h].Release();
                return false;
            }

            _locks[h].Release();

            __message = "HashLocks_Find";
            __caller = _caller;
            __line = _line;
            return true;
        }


        public async Task<int> Add(T value,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;

            h = value.Hash() % _bucket;
            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Add, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (true == _table[h].ContainsKey(value.IID()))
            {
                _locks[h].Release();
                return -2;
            }

            _table[h].Add(value.IID(), value);
            Interlocked.Increment(ref _count);

            _locks[h].Release();

            __message = "HashLocks_Add";
            __caller = _caller;
            __line = _line;

            return 0;
        }

        public async Task<int> Remove(IHash<K> key,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            int rc = 0;

            long h;
            h = key.Hash() % _bucket;

            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Remove, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (true == _table[h].Remove(key.IID()))
            {
                Interlocked.Decrement(ref _count);
                rc = 1;
            }
            _locks[h].Release();

            __message = "HashLocks_Remove";
            __caller = _caller;
            __line = _line;

            return rc;
        }

        public async Task<int> Replace(T value,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;

            h = value.Hash() % _bucket;
            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Replace, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (true == _table[h].Remove(value.IID()))
                Interlocked.Decrement(ref _count);

            _table[h].Add(value.IID(), value);
            Interlocked.Increment(ref _count);

            _locks[h].Release();

            __message = "HashLocks_Replace";
            __caller = _caller;
            __line = _line;
            return 0;
        }

        public async Task<(bool result, T value)> TryGetValue(IHash<K> key,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;
            h = key.Hash() % _bucket;

            var value = default(T);
            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_TryGetValue, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (false == _table[h].TryGetValue(key.IID(), out value))
            {
                _locks[h].Release();
                return (false, value);
            }

            _locks[h].Release();

            __message = "HashLocks_TryGetValue";
            __caller = _caller;
            __line = _line;

            return (true, value);
        }

        public async Task<int> Count(
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            int cnt = 0;
            for (int i = 0; i < _bucket; i++)
            {
                if (0 >= _table[i].Count)
                    continue;

                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Count, {3}:{4}", __message, __caller, __line, _caller, _line));

                cnt += _table[i].Count;
                _locks[i].Release();
            }

            __message = "HashLocks_Count";
            __caller = _caller;
            __line = _line;

            return cnt;
        }

        public async Task<List<T>> Listup(
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            var lists = new List<T>();
            for (int i = 0; i < _bucket; i++)
            {
                if (0 >= _table[i].Count)
                    continue;

                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Listup, {3}:{4}", __message, __caller, __line, _caller, _line));

                if (0 < _table[i].Count)
                    lists.AddRange(_table[i].Values);

                _locks[i].Release();
            }

            __message = "HashLocks_Listup";
            __caller = _caller;
            __line = _line;

            return lists;
        }

        public async Task<List<T>> FindListup(IHash<K>[] keys,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            var lists = new List<T>();
            foreach (var key in keys)
            {
                var i = key.Hash() % _bucket;
                if (0 < _table[i].Count)
                    continue;

                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_FindListup, {3}:{4}", __message, __caller, __line, _caller, _line));

                if (true == _table[i].ContainsKey(key.IID()))
                    lists.Add(_table[i][key.IID()]);

                _locks[i].Release();
            }

            __message = "HashLocks_FindListup";
            __caller = _caller;
            __line = _line;

            return lists;
        }


        public async Task<Dictionary<K, T>> EnterLock(int h,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            // lock.. need
            //Console.WriteLine("Try EnterLock.. {0}", h);
            if (true != await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_EnterLock, {3}:{4}", __message, __caller, __line, _caller, _line));

            __message = "HashLocks_EnterLock";
            __caller = _caller;
            __line = _line;

            return _table[h];
        }

        public void LeaveLock(int h)
        {
            // lock.. need 
            //Console.WriteLine("Try LeaveLock.. {0}", h);
            _locks[h].Release();
            return;
        }

        public async Task<List<T>> Listup(int h,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            List<T> ll = new List<T>();

            // lock.. need
            //Console.WriteLine("Try EnterLock.. {0}", h);

            if (true != await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_EnterLock, {3}:{4}", __message, __caller, __line, _caller, _line));

            ll.AddRange(_table[h].Values);


            _locks[h].Release();

            __message = "HashLocks_Listup_h";
            __caller = _caller;
            __line = _line;

            return ll;
        }
    }

    public class TreeLocks<T>
    {
        public long _count { get; private set; }
        public long _bucket { get; private set; }
        public int _timeout { get; private set; }

        private string __message = string.Empty;
        private string __caller = string.Empty;
        private int __line = 0;


        public SemaphoreSlim[] _locks { get; private set; }
        public Dictionary<long, T>[] _table { get; private set; }

        public TreeLocks()
        {
            _count = 0;
            _bucket = 37;
            _timeout = 3000;
            _locks = new SemaphoreSlim[_bucket];
            _table = new Dictionary<long, T>[_bucket];

            int i;
            for (i = 0; i < _bucket; i++)
            {
                _locks[i] = new SemaphoreSlim(1, 5);
                _table[i] = new Dictionary<long, T>();
            }
        }

        public TreeLocks(int slot, int timeout)
        {
            _count = 0;
            _bucket = slot;

            _timeout = timeout;
            if (1000 > timeout)
                _timeout = 1000;
            if (30000 < timeout)
                _timeout = 30000;

            _locks = new SemaphoreSlim[_bucket];
            _table = new Dictionary<long, T>[_bucket];

            int i;
            for (i = 0; i < _bucket; i++)
            {
                _locks[i] = new SemaphoreSlim(1, 1);
                _table[i] = new Dictionary<long, T>();
            }
        }

        public async Task Clear(
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            int i;
            for (i = 0; i < _bucket; i++)
            {
                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Clear, {3}:{4}", __message, __caller, __line, _caller, _line));

                _table[i].Clear();
                _locks[i].Release();
            }
            _count = 0;

            __message = "HashLocks_Clear";
            __caller = _caller;
            __line = _line;
        }

        public async Task<bool> Find(long key,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;
            h = key % _bucket;

            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Find, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (true != _table[h].ContainsKey(key))
            {
                _locks[h].Release();
                return false;
            }

            _locks[h].Release();

            __message = "HashLocks_Find";
            __caller = _caller;
            __line = _line;
            return true;
        }


        public async Task<int> Add(long key, T value,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;

            h = key % _bucket;
            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Add, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (true == _table[h].ContainsKey(key))
            {
                _locks[h].Release();
                return -2;
            }

            _table[h].Add(key, value);
            _count++;

            _locks[h].Release();

            __message = "HashLocks_Add";
            __caller = _caller;
            __line = _line;

            return 0;
        }

        public async Task<int> Remove(long key,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            int rc = 0;

            long h;
            h = key % _bucket;

            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Remove, {3}:{4}", __message, __caller, __line, _caller, _line));

            var found = _table[h].Remove(key);
            if (true == found)
            {
                rc = 1;
                _count--;
            }
            _locks[h].Release();

            __message = "HashLocks_Remove";
            __caller = _caller;
            __line = _line;
            return rc;
        }

        public async Task<int> Replace(long key, T value,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;

            h = key % _bucket;
            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Replace, {3}:{4}", __message, __caller, __line, _caller, _line));

            _table[h].Remove(key);
            _table[h].Add(key, value);

            _locks[h].Release();

            __message = "HashLocks_Replace";
            __caller = _caller;
            __line = _line;
            return 0;
        }

        public async Task<(bool result, T value)> TryGetValue( long key,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            long h;
            h = key % _bucket;

            var value = default(T);
            if (false == await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_TryGetValue, {3}:{4}", __message, __caller, __line, _caller, _line));

            if (false == _table[h].TryGetValue(key, out value))
            {
                _locks[h].Release();
                return (false, value);
            }

            _locks[h].Release();

            __message = "HashLocks_TryGetValue";
            __caller = _caller;
            __line = _line;

            return (true, value);
        }

        public async Task<int> Count(
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            int cnt = 0;
            for (int i = 0; i < _bucket; i++)
            {
                if (0 >= _table[i].Count)
                    continue;

                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Count, {3}:{4}", __message, __caller, __line, _caller, _line));

                cnt += _table[i].Count;
                _locks[i].Release();
            }

            __message = "HashLocks_Count";
            __caller = _caller;
            __line = _line;

            return cnt;
        }

        public async Task<List<T>> Listup(
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            var lists = new List<T>();
            for (int i = 0; i < _bucket; i++)
            {
                if (0 >= _table[i].Count)
                    continue;

                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_Listup, {3}:{4}", __message, __caller, __line, _caller, _line));

                if (0 < _table[i].Count)
                    lists.AddRange(_table[i].Values);

                _locks[i].Release();
            }

            __message = "HashLocks_Listup";
            __caller = _caller;
            __line = _line;

            return lists;
        }

        public async Task<List<T>> FindListup(long[] keys,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            var lists = new List<T>();
            foreach (var key in keys)
            {
                var i = key % _bucket;
                if (0 < _table[i].Count)
                    continue;

                if (false == await _locks[i].WaitAsync(_timeout))
                    throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_FindListup, {3}:{4}", __message, __caller, __line, _caller, _line));

                if (true == _table[i].ContainsKey(key))
                    lists.Add(_table[i][key]);

                _locks[i].Release();
            }

            __message = "HashLocks_FindListup";
            __caller = _caller;
            __line = _line;

            return lists;
        }


        public async Task<Dictionary<long, T>> EnterLock(int h,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            // lock.. need
            //Console.WriteLine("Try EnterLock.. {0}", h);
            if (true != await _locks[h].WaitAsync(_timeout))
                throw new TimeoutException(string.Format("\nPast: {0}, {1}:{2}\nCall: HashLocks_EnterLock, {3}:{4}", __message, __caller, __line, _caller, _line));

            __message = "HashLocks_EnterLock";
            __caller = _caller;
            __line = _line;

            return _table[h];
        }

        public void LeaveLock(int h)
        {
            // lock.. need 
            //Console.WriteLine("Try LeaveLock.. {0}", h);
            _locks[h].Release();
            return;
        }
    }
}
