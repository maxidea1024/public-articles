using System;

namespace G.Util
{
	public class SimpleTimer
	{
		private DateTime checkedTime;

		public bool IsStarted { get; private set; }
		public TimeSpan ElapsedTime { get; private set; }

		public SimpleTimer()
		{
			Reset();
		}

		public void Reset()
		{
			checkedTime = DateTime.Now;
			ElapsedTime = TimeSpan.Zero;
		}

		public void Start()
		{
			if (IsStarted)
			{
				Check();
			}
			else
			{
				IsStarted = true;
				checkedTime = DateTime.Now;
			}
		}

		public TimeSpan Stop()
		{
			var elapsed = Check();
			IsStarted = false;
			return elapsed;
		}

		public TimeSpan Check()
		{
			if (IsStarted)
			{
				var now = DateTime.Now;

				var elapsed = now - checkedTime;
				if (elapsed > TimeSpan.Zero)
				{
					ElapsedTime += elapsed;
				}

				checkedTime = now;
			}

			return ElapsedTime;
		}

		public TimeSpan CheckAndReset()
		{
			var elapsedTime = Check();
			Reset();
			return elapsedTime;
		}

		public bool CheckAndReset(TimeSpan timeout)
		{
			var elapsedTime = Check();

			if (timeout > TimeSpan.Zero)
			{
				if (elapsedTime >= timeout)
				{
					Reset();
					return true;
				}
			}

			return false;
		}
	}
}
