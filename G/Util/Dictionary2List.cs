using System.Collections;
using System.Collections.Generic;

namespace G.Util
{
	public class Dictionary2List<T, U, V> : IEnumerable<KeyValuePair<T, DictionaryList<U, V>>>
	{
		private Dictionary<T, DictionaryList<U, V>> dic = new Dictionary<T, DictionaryList<U, V>>();
		public Dictionary<T, DictionaryList<U, V>>.KeyCollection Keys => dic.Keys;
		public Dictionary<T, DictionaryList<U, V>>.ValueCollection Values => dic.Values;
		public int Count => dic.Count;

		public void Clear()
		{
			dic = new Dictionary<T, DictionaryList<U, V>>();
		}

		public DictionaryList<U, V> Find(T t)
		{
			DictionaryList<U, V> dicList;
			if (dic.TryGetValue(t, out dicList))
				return dicList;
			else
				return null;
		}

		public V[] Find(T t, U u)
		{
			DictionaryList<U, V> dicList;
			if (dic.TryGetValue(t, out dicList))
			{
				return dicList.Find(u);
			}

			return null;
		}

		public void Add(T t, U u, V v)
		{
			DictionaryList<U, V> dicList;
			if (dic.TryGetValue(t, out dicList) == false)
			{
				dicList = new DictionaryList<U, V>();
				dic.Add(t, dicList);
			}

			dicList.Add(u, v);
		}

		public bool Remove(T t)
		{
			return dic.Remove(t);
		}

		public bool Remove(T t, U u)
		{
			DictionaryList<U, V> dicList;
			if (dic.TryGetValue(t, out dicList) == false)
				return false;

			if (dicList.Remove(u) == false)
				return false;

			if (dicList.Count == 0)
				dic.Remove(t);

			return true;
		}

		public bool Remove(T t, U u, V v)
		{
			DictionaryList<U, V> dicList;
			if (dic.TryGetValue(t, out dicList) == false)
				return false;

			if (dicList.Remove(u, v) == false)
				return false;

			if (dicList.Count == 0)
				dic.Remove(t);

			return true;
		}

		#region IEnumerable
		public IEnumerator<KeyValuePair<T, DictionaryList<U, V>>> GetEnumerator()
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
