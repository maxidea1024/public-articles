using System;
using System.Collections.Generic;

namespace G.Util
{
	public class Ranker<T> where T : class, IComparable<T>
	{
		public class Rank
		{
			public int Ranking { get; set; }
			public T Object { get; set; }
		}

		public List<T> List { get; private set; } = new List<T>();

		public int Count { get { return List.Count; } }

		public void Reset()
		{
			List.Clear();
		}

		public void Add(T player)
		{
			List.Add(player);
		}

		public List<Rank> Sort(bool reverse = false)
		{
			List.Sort();
			if (reverse) List.Reverse();

			var ranks = new List<Rank>(List.Count);

			int ranking = 1;
			int skip = 1;
			T previous = null;

			foreach (var i in List)
			{
				var rank = new Rank { Object = i };

				if (previous == null)
				{
					rank.Ranking = ranking;
				}
				else if (i.CompareTo(previous) == 0)
				{
					skip++;
					rank.Ranking = ranking;
				}
				else
				{
					ranking += skip;
					skip = 1;
					rank.Ranking = ranking;
				}

				ranks.Add(rank);

				previous = i;
			}

			return ranks;
		}
	}
}
