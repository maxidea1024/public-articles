using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using G.Util;

namespace G.DataSet
{
    public interface IHash<K>
    {
        K IID();
        long Hash();
    }

    public class HashData : IHash<long>
    {
        public long id;

        public long IID() { return id; }
        public long Hash() { return id; }
    }

    public interface IDataSet<T>
    {
    };

    public class DataSet<T> : IDataSet<T>
    {
        protected HashSet<T> _hash = new HashSet<T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }

        public virtual bool Add(T item) { return false; }
        public virtual bool Remove(T item) { return false; }
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }

        public virtual async Task<bool> AddAsync(T item) { await Task.Delay(0); return false; }
        public virtual async Task<bool> RemoveAsync(T item) { await Task.Delay(0); return false; }
        public virtual async Task<List<T>> ListsAsync() { await Task.Delay(0); return null; }
        public virtual async Task<List<T>> ListupAsync() { await Task.Delay(0); return null; }
    };

    public class DataSorted<T> : IDataSet<T>
    {
        protected SortedSet<T> _sorted= new SortedSet<T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }
        public virtual bool Add(T item) { return false; }
        public virtual bool Remove(T item) { return false; }
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }
    };

    public class DataStack<T> : IDataSet<T>
    {
        protected Stack<T> _stack = new Stack<T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }
        public virtual void Add(T item) { }
        public virtual bool TryPeek(out T data) { data = default(T); return false; }
        public virtual bool Take(out T data) { data = default(T); return false; }
    };

    public class DataQueue<T> : IDataSet<T>
    {
        protected Queue<T> _queue = new Queue<T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }
        public virtual void Add(T item) { }
        public virtual bool TryPeek(out T data) { data = default(T); return false; }

        public virtual bool Take(out T data) { data = default(T); return false; }
        public virtual bool TryTake(out T data, int wait = 0) { data = default(T); return false; }
    };

    public class DataListed<T> : IDataSet<T>
    {
        protected List<T> _listed = new List<T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }
        public virtual void Add(T data) { }
        public virtual bool Remove(T data) { return false; }
        public virtual bool TryPeek(out T data) { data = default(T); return false; }
        public virtual bool Take( out T data) { data = default(T); return false; }        
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }
    };

    public class DataLinked<T> : IDataSet<T>
    {
        protected LinkedList<T> _listed = new LinkedList<T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }
        public virtual bool Add(T data) { return false; }
        public virtual bool Remove(T data) { return false; }
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }
    };

    public class DataRanked<K, T> : IDataSet<T> where T : IHash<K>
    {
        protected SortedList<K, T> _ranked = new SortedList<K, T>(); // duplicate, no ordered..

        public virtual long Counting() { return 0; }
        public virtual bool Add(T data) { return false; }
        public virtual bool Remove(K key) { return false; }
        public virtual bool Remove(T data) { return false; }
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }
    };

    public class DataHashed<K, T> : IDataSet<T> where T : IHash<K>
    {
        protected Hashtable _hash = new Hashtable();

        public virtual long Counting() { return 0; }
        public virtual bool Add(T data) { return false; }
        public virtual bool Remove(K key) { return false; }
        public virtual bool Remove(T data) { return false; }
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }
    };

    public class DataTree<K, T> : IDataSet<T> where T : IHash<K>
    {
        protected Dictionary<K, T> _tree = new Dictionary<K, T>();

        public virtual long Counting() { return 0; }
        public virtual T Find(K key) { return default(T); }
        public virtual bool Add(T data) { return false; }
        public virtual bool Remove(K key) { return false; }
        public virtual bool Remove(T item) { return false; }
        public virtual List<T> Lists() { return null; }
        public virtual List<T> Listup() { return null; }
    };
}