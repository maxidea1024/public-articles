
ConcurrentDictionary<int,int> cd = new ConcurrentDictionary<int,int>();

    cd.AddOrUpdate(1, 1, (key, oldValue) => oldValue + 1);

    cd.TryRemove(TKey, TValue);

    bool cd.TryGetValue(TKey key, out TValue value);



var stack = new ConcurrentStack<int>();

    stack.TryPeek


var queue = new ConcurrentQueue<int>();


