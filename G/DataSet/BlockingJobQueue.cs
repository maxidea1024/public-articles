using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace G.DataSet
{
	public class BlockingJobQueue<T>
	{
		private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
		//public BlockingCollection<T> _jobQueue = new BlockingCollection<T>();
        //public ConcurrentQueue<T> _jobQueue = new ConcurrentQueue<T>();
        public SpinQueue<T> _jobQueue = new SpinQueue<T>();

        public delegate Task JobDelegateAsync(T data);
		public JobDelegateAsync _jobProcessAsync;

		protected int _jobTasks = 1;
		protected int __pushs;
		protected int __takes;

		public BlockingJobQueue(int tasks, JobDelegateAsync proc)
		{
			Type t = GetType();

			log.Debug("BlockingJobQueue..{0}, {1}, {2}", tasks, proc.Method.Name, GetType().GetGenericArguments()[0].Name);

            _jobTasks = tasks;
            //_jobTasks = 1;
            _jobProcessAsync = proc;
        }

		public void Run(bool isWatchUp = false)
		{
			for (int i = 0; i < _jobTasks; i++)
				Task.Run( () => RunBlockingJobAsync());

			if (false == isWatchUp)
				return;

			Task.Run(() => WatchJobAsync());
		}

		public async Task WatchJobAsync()
		{
			int _p = __pushs;
			int _t = __takes;

			while (true)
			{
				await Task.Delay(1000);

				try
				{
					log.Debug("BlockingJobQueue: {0}, {1}, {2}", _jobQueue.Counting(), __pushs - _p, __takes - _t);

					_p = __pushs;
					_t = __takes;
				}
				catch (Exception e)
				{
					log.Error(e);
				}
			}
		}


		public void EnqueueJob(T job)
		{
            _jobQueue.Add(job);
			__pushs++;
		}

		public async Task RunBlockingJobAsync()
		{
            bool __sucess = false;
            T job = default(T);
			while (true)
			{
				try
				{
					__sucess = _jobQueue.Take(out job);
                    if (false == __sucess)
                    {
                        await Task.Delay(50);
                        continue;
                    }
					__takes++;

					//log.Debug("DBJobQueue: {0}, {1}", _jobQueue.GetType().GetGenericArguments()[0].Name, _jobQueue.Count);
					await _jobProcessAsync(job);
				}
				catch (Exception ex)
				{
					log.Error(ex);
				}
			}
		}
	}
}