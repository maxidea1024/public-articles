using System.Collections;
using System.Collections.Generic;

namespace G.Util
{
	public class Dictionary2Counter<K1, K2> : IEnumerable<KeyValuePair<K1, DictionaryCounter<K2>>>
	{
		private Dictionary<K1, DictionaryCounter<K2>> dic = new Dictionary<K1, DictionaryCounter<K2>>();
		public Dictionary<K1, DictionaryCounter<K2>>.KeyCollection Keys => dic.Keys;
		public Dictionary<K1, DictionaryCounter<K2>>.ValueCollection Values => dic.Values;
		public int Count => dic.Count;

		public void Clear()
		{
			dic = new Dictionary<K1, DictionaryCounter<K2>>();
		}

		public DictionaryCounter<K2> Find(K1 k)
		{
			if (dic.TryGetValue(k, out var dic2))
				return dic2;
			else
				return null;
		}

		public int Find(K1 k1, K2 k2)
		{
			var dic2 = Find(k1);
			if (dic2 == null) return 0;

			return dic2.Find(k2);
		}

		public int Add(K1 k1, K2 k2, int count = 1)
		{
			var dic2 = Find(k1);
			if (dic2 == null) dic2 = new DictionaryCounter<K2>();

			return dic2.Add(k2, count);
		}

		public bool Remove(K1 k1)
		{
			return dic.Remove(k1);
		}

		public bool Remove(K1 k1, K2 k2)
		{
			var dic2 = Find(k1);
			if (dic2 == null) return false;

			return dic2.Remove(k2);
		}

		#region IEnumerable
		public IEnumerator<KeyValuePair<K1, DictionaryCounter<K2>>> GetEnumerator()
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
