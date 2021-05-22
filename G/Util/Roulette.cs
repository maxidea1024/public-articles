using System;
using System.Collections.Generic;

namespace G.Util
{
	public interface IRoulette
	{
		int Probability { get; }
	}

	public class Roulette<T>
	{
		private readonly Randomizer random = new Randomizer();

		private readonly List<T> listKey = new List<T>();
		private readonly List<int> listValue = new List<int>();
		private int totalValue;

		public Roulette() {}

		public Roulette(IEnumerable<T> list)
		{
			Add(list);
		}

		public Roulette(IEnumerable<T> list, int value)
		{
			Add(list, value);
		}

		public int Count
		{
			get
			{
				return listKey.Count;
			}
		}

		public T[] Keys
		{
			get
			{
				return listKey.ToArray();
			}
		}

		public int[] Values
		{
			get
			{
				return listValue.ToArray();
			}
		}

		public KeyValuePair<T, int> this[int i]
		{
			get
			{
				return new KeyValuePair<T, int>(listKey[i], listValue[i]);
			}
		}

		public void Clear()
		{
			listKey.Clear();
			listValue.Clear();
			totalValue = 0;
		}

		private void _Add(T key, int value)
		{
			if (value < 0) return;
			totalValue += value;

			listKey.Add(key);
			listValue.Add(value);
		}

		public void Add(T key, int value)
		{
			_Add(key, value);
		}

		public void Add(T obj)
		{
			_Add(obj, ((IRoulette)obj).Probability);
		}

		public void Add(IEnumerable<T> list)
		{
			foreach (var item in list)
			{
				_Add(item, ((IRoulette)item).Probability);
			}
		}

		public void Add(IEnumerable<T> list, int value)
		{
			foreach (var item in list)
			{
				_Add(item, value);
			}
		}

		public void RemoveAt(int index)
		{
			if (index >= listKey.Count) return;
			totalValue -= listValue[index];

			listKey.RemoveAt(index);
			listValue.RemoveAt(index);
		}

		public T GetNext(bool remove = false)
		{
			int n = random.Next(totalValue);
			int count = listKey.Count;
			int sum = 0;

			for (int i = 0; i < count; i++)
			{
				sum += listValue[i];
				if (n < sum)
				{
					T key = listKey[i];
					if (remove) RemoveAt(i);
					return key;
				}
			}

			throw new Exception("It is Impossible Situation");
		}

		public IEnumerator<KeyValuePair<T, int>> GetEnumerator()
		{
			int count = listKey.Count;
			for (int i = 0; i < count; i++)
			{
				yield return new KeyValuePair<T, int>(listKey[i], listValue[i]);
			}
		}

		public Roulette<T> Clone()
		{
			Roulette<T> newRoulette = new Roulette<T>();
			newRoulette.listKey.AddRange(listKey);
			newRoulette.listValue.AddRange(listValue);
			newRoulette.totalValue = totalValue;

			return newRoulette;
		}

		public void IncreaseProbability(int percent, params T[] exceptedKeys)
		{
			int count = listValue.Count;

			for (int i = 0; i < count; i++)
			{
				foreach (T t in exceptedKeys)
				{
					if (t.Equals(listKey[i])) goto label1;
				}

				int n = listValue[i] * percent / 100;
				listValue[i] += n;
				totalValue += n;

				label1:;
			}
		}
	}
}
