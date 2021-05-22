using System;
using System.Collections.Generic;

namespace G.Util
{
	public interface IRentable
	{
		IntId Id { get; set; }
	}

	public class Rental<T> where T : IRentable, new ()
	{
		protected List<T> allList = new List<T>();
		protected Queue<IntId> checkInQueue = new Queue<IntId>();
		protected HashSet<IntId> checkOutSet = new HashSet<IntId>();

		public int Max { get; protected set; }
		public int CheckOutCount { get { return checkOutSet.Count; } }
		public int CheckInCount { get { return checkInQueue.Count; } }

		public Rental(int max = int.MaxValue)
		{
			Max = max;
		}

		public void Reset()
		{
			allList.Clear();
			checkInQueue.Clear();
			checkOutSet.Clear();
		}

		public T CheckOut()
		{
			try
			{
				T t;

				if (checkInQueue.Count <= 0)
				{
					if (allList.Count >= Max)
						return default(T);

					var id = (IntId)allList.Count;

					t = new T();
					t.Id = id;

					checkOutSet.Add(id);
					allList.Add(t);
				}
				else
				{
					var id = checkInQueue.Dequeue();

					checkOutSet.Add(id);
					t = allList[id.Index];
				}

				return t;
			}
			catch (InvalidOperationException)
			{
				return default(T);
			}
		}

		public bool CheckIn(T t)
		{
			var id = t.Id;

			if (checkOutSet.Remove(id))
			{
				id.Refresh();
				t.Id = id;

				checkInQueue.Enqueue(id);

				return true;
			}

			return false;
		}

		public T Find(IntId id)
		{
			if (checkOutSet.Contains(id))
				return allList[id.Index];
			else
				return default(T);
		}
	}
}
