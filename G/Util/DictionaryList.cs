using System.Collections;
using System.Collections.Generic;

namespace G.Util
{
	public class DictionaryList<K, T> : IEnumerable<KeyValuePair<K, List<T>>>
	{
		private Dictionary<K, List<T>> dic = new Dictionary<K, List<T>>();
		public Dictionary<K, List<T>>.KeyCollection Keys => dic.Keys;
		public Dictionary<K, List<T>>.ValueCollection Values => dic.Values;
		public int Count => dic.Count;

		public void Clear()
		{
			dic = new Dictionary<K, List<T>>();
		}

		public T[] Find(K k)
		{
			List<T> list;
			if (dic.TryGetValue(k, out list))
				return list.ToArray();
			else
				return null;
		}

		public T Find(K k, int index)
		{
			List<T> list;
			if (dic.TryGetValue(k, out list))
			{
				if (index < list.Count)
					return list[index];
			}

			return default(T);
		}

		public void Add(K k, T t)
		{
			List<T> list;
			if (dic.TryGetValue(k, out list) == false)
			{
				list = new List<T>();
				dic.Add(k, list);
			}

			list.Add(t);
		}

		public bool Remove(K k)
		{
			return dic.Remove(k);
		}

		public bool Remove(K k, T t)
		{
			List<T> list;
			if (dic.TryGetValue(k, out list) == false)
				return false;

			if (list.Remove(t) == false)
				return false;

			if (list.Count == 0)
				dic.Remove(k);

			return true;
		}

		#region IEnumerable
		public IEnumerator<KeyValuePair<K, List<T>>> GetEnumerator()
		{
			return dic.GetEnumerator();
		}

		private IEnumerator GetEnumerator1()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator1();
		}
		#endregion
	}
}
