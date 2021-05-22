using System.Collections.Generic;

namespace G.Util
{
	public class Dictionary2<K1, K2, T> where T : class
	{
		private Dictionary<K1, Dictionary<K2, T>> dic = new Dictionary<K1, Dictionary<K2, T>>();
		public int Count => dic.Count;

		public void Clear()
		{
			dic = new Dictionary<K1, Dictionary<K2, T>>();
		}

		public Dictionary<K2, T> Find(K1 k1)
		{
			Dictionary<K2, T> subDic;
			if (dic.TryGetValue(k1, out subDic))
				return subDic;
			else
				return null;
		}

		public T Find(K1 k1, K2 k2)
		{
			Dictionary<K2, T> subDic;
			if (dic.TryGetValue(k1, out subDic))
			{
				T t = null;
				subDic.TryGetValue(k2, out t);
				return t;
			}
			return null;
		}

		public void Add(K1 k1, K2 k2, T t)
		{
			Dictionary<K2, T> subDic;
			if (dic.TryGetValue(k1, out subDic) == false)
			{
				subDic = new Dictionary<K2, T>();
				dic.Add(k1, subDic);
			}

			subDic[k2] = t;
		}

		public bool Remove(K1 k1)
		{
			return dic.Remove(k1);
		}

		public bool Remove(K1 k1, K2 k2)
		{
			Dictionary<K2, T> subDic;
			if (dic.TryGetValue(k1, out subDic) == false)
				return false;

			if (subDic.Remove(k2) == false)
				return false;

			if (subDic.Count == 0)
				dic.Remove(k1);

			return true;
		}
	}
}
