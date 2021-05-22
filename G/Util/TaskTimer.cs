using System;
using System.Threading;
using System.Threading.Tasks;

namespace G.Util
{
    public class TaskTimer
    {
		private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		public Func<Task> Function { get; private set; }
        public Action Action { get; private set; }
        public TimeSpan DueTime { get; private set; } = TimeSpan.Zero;
        public TimeSpan Period { get; private set; } = TimeSpan.MaxValue;
        private CancellationTokenSource cts;

        public TaskTimer(Func<Task> function, int dueTime, int period)
        {
            Function = function;
            DueTime = TimeSpan.FromMilliseconds(dueTime);
            Period = TimeSpan.FromMilliseconds(period);
        }

        public TaskTimer(Func<Task> function, TimeSpan dueTime, TimeSpan period)
        {
            Function = function;
            DueTime = dueTime;
            Period = period;
        }

        public TaskTimer(Action action, int dueTime, int period)
        {
            Action = action;
            DueTime = TimeSpan.FromMilliseconds(dueTime);
            Period = TimeSpan.FromMilliseconds(period);
        }

        public TaskTimer(Action action, TimeSpan dueTime, TimeSpan period)
        {
            Action = action;
            DueTime = dueTime;
            Period = period;
        }

        public void Start()
        {
            Stop();
            cts = new CancellationTokenSource();
            Run();
        }

        public void Start(int cancellationDelay)
        {
            Stop();
            cts = new CancellationTokenSource(cancellationDelay);
            Run();
        }

        public void Start(TimeSpan cancellationDelay)
        {
            Stop();
            cts = new CancellationTokenSource(cancellationDelay);
            Run();
        }

        public void Stop()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        private void Run()
        {
            if (Function != null)
                Task.Run(async () => await RunAsync(Function, DueTime, Period, cts.Token));
            else
                Task.Run(() => RunAsync(Action, DueTime, Period, cts.Token));
        }

        public static async Task RunAsync(Func<Task> func, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(dueTime, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    await func();
                }
            }
            catch (TaskCanceledException) { }

            if (period != TimeSpan.Zero)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(period, cancellationToken);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await func();
                        }
                    }
					catch (TaskCanceledException)
					{
						break;
					}
                    catch (Exception ex)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
						log.Debug(ex.Message);
                    }
                }
            }
        }

        public static async Task RunAsync(Func<Task> function, int dueTime, int period, CancellationToken cancellationToken)
        {
            TimeSpan p = TimeSpan.Zero;
            if (period > 0) p = TimeSpan.FromMilliseconds(period);

            await RunAsync(function, TimeSpan.FromMilliseconds(dueTime), p, cancellationToken);
        }

        public static async Task RunAsync(Action action, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(dueTime, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Run(() => action());
                }
            }
            catch (TaskCanceledException) { }

            if (period != TimeSpan.Zero)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(period, cancellationToken);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Run(() => action());
                        }
                    }
                    catch(Exception ex)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
						log.Debug(ex.Message);
                    }
                }
            }
        }

        public static async Task RunAsync(Action action, int dueTime, int period, CancellationToken cancellationToken)
        {
            TimeSpan p = TimeSpan.Zero;
            if (period > 0) p = TimeSpan.FromMilliseconds(period);

            await RunAsync(action, TimeSpan.FromMilliseconds(dueTime), p, cancellationToken);
        }
    }
}
