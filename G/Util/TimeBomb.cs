using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace G.Util
{
	public delegate void TimeBombCallback();

	public class TimeBomb
	{
		private Func<Task> function;
		private Action action;
		private CancellationTokenSource cancellation;

		public TimeSpan Time { get; private set; } = TimeSpan.Zero;

		public TimeBomb(Func<Task> function, int time) : this(function, TimeSpan.FromMilliseconds(time))
		{
		}

		public TimeBomb(Func<Task> function, TimeSpan time)
		{
			this.function = function;
			Time = time;
		}

		public TimeBomb(Action action, int time) : this(action, TimeSpan.FromMilliseconds(time))
		{
		}

		public TimeBomb(Action action, TimeSpan time)
		{
			this.action = action;
			Time = time;
		}

		public void Start()
		{
			Start(Time);
		}

		public void Start(int time)
		{
			Start(TimeSpan.FromMilliseconds(time));
		}

		public void Start(TimeSpan time)
		{
			Stop();

			cancellation = new CancellationTokenSource();
			Time = time;

			Task.Run(async () =>
			{
				try
				{
					await Task.Delay(Time, cancellation.Token);

					Interlocked.Exchange(ref cancellation, null);

					if (function != null) await function();
					if (action != null) action();
				}
				catch (TaskCanceledException) { }
			}, cancellation.Token);
		}

		public bool Stop()
		{
			bool result = false;

			try
			{
				var oldCts = Interlocked.Exchange(ref cancellation, null);
				if (oldCts != null)
				{
					oldCts.Cancel();
					result = true;
				}
			}
			catch { }

			return result;
		}

		public bool Reset()
		{
			var result = Stop();
			if (result) Start();
			return result;
		}

		public void Explode()
		{
			if (Stop())
			{
				if (function != null) function().Wait();
				if (action != null) action();
			}
		}
	}
}
