using System.Collections;
using System.Collections.Generic;

namespace G.Util
{
	public class DictionarySortedList<K1, K2, T> : IEnumerable<KeyValuePair<K1, SortedList<K2, T>>>
	{
		private Dictionary<K1, SortedList<K2, T>> dic = new Dictionary<K1, SortedList<K2, T>>();
		public Dictionary<K1, SortedList<K2, T>>.KeyCollection Keys => dic.Keys;
		public Dictionary<K1, SortedList<K2, T>>.ValueCollection Values => dic.Values;
		public int Count => dic.Count;

		public void Clear()
		{
			dic = new Dictionary<K1, SortedList<K2, T>>();
		}

		public IEnumerator<KeyValuePair<K2, T>> GetEnumerator(K1 k)
		{
			if (dic.TryGetValue(k, out var list))
				return list.GetEnumerator();
			else
				return null;
		}

		public SortedList<K2, T> Find(K1 k)
		{
			if (dic.TryGetValue(k, out var list))
				return list;
			else
				return null;
		}

		public T Find(K1 k1, K2 k2)
		{
			if (dic.TryGetValue(k1, out var list))
			{
				if (list.TryGetValue(k2, out var value))
					return value;
			}

			return default(T);
		}

		public void Add(K1 k1, K2 k2, T t)
		{
			if (dic.TryGetValue(k1, out var list) == false)
			{
				list = new SortedList<K2, T>();
				dic.Add(k1, list);
			}

			list.Add(k2, t);
		}

		public bool Remove(K1 k)
		{
			return dic.Remove(k);
		}

		public bool Remove(K1 k1, K2 k2)
		{
			if (dic.TryGetValue(k1, out var list) == false)
				return false;

			if (list.Remove(k2) == false)
				return false;

			if (list.Count == 0)
				dic.Remove(k1);

			return true;
		}

		#region IEnumerable
		public IEnumerator<KeyValuePair<K1, SortedList<K2, T>>> GetEnumerator()
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
