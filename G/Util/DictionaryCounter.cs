using System.Collections;
using System.Collections.Generic;

namespace G.Util
{
	public class DictionaryCounter<K> : IEnumerable<KeyValuePair<K, int>>
	{
		private Dictionary<K, int> dic = new Dictionary<K, int>();
		public Dictionary<K, int>.KeyCollection Keys => dic.Keys;
		public Dictionary<K, int>.ValueCollection Values => dic.Values;
		public int Count => dic.Count;

		public void Clear()
		{
			dic = new Dictionary<K, int>();
		}

		public int Find(K k)
		{
			if (dic.TryGetValue(k, out var count))
				return count;
			else
				return 0;
		}

		public int Add(K k, int count = 1)
		{
			int totalCount;

			if (dic.TryGetValue(k, out int value))
			{
				totalCount = value + count;
				dic[k] = totalCount;
			}
			else
			{
				totalCount = count;
				dic.Add(k, totalCount);
			}

			return totalCount;
		}

		public bool Remove(K k)
		{
			return dic.Remove(k);
		}

		#region IEnumerable
		public IEnumerator<KeyValuePair<K, int>> GetEnumerator()
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
