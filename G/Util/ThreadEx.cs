using System.Threading;

namespace G.Util
{
	public class ThreadEx
	{
		public static void SetupThreadPool(int minWorkerThreads, int minCompletionPortThreads, int maxWorkerThreads = 30000, int maxCompletionPortThreads = 10000)
		{
			int minWorker;
			int minCompletionPort;
			ThreadPool.GetMinThreads(out minWorker, out minCompletionPort);

			int maxWorker;
			int maxCompletionPort;
			ThreadPool.GetMaxThreads(out maxWorker, out maxCompletionPort);

			if (minWorker < minWorkerThreads) minWorker = minWorkerThreads;
			if (minCompletionPort < minCompletionPortThreads) minCompletionPort = minCompletionPortThreads;
			if (maxWorker < maxWorkerThreads) maxWorker = maxWorkerThreads;
			if (maxCompletionPort < maxCompletionPortThreads) maxCompletionPort = maxCompletionPortThreads;

			ThreadPool.SetMinThreads(minWorker, minCompletionPort);
			ThreadPool.SetMaxThreads(maxWorker, maxCompletionPort);
		}

		public static void LogThreadPool(NLog.Logger log)
		{
			int minWorker;
			int minCompletionPort;
			ThreadPool.GetMinThreads(out minWorker, out minCompletionPort);
			log.Info("Min WorkerThreads = {0}, Min CompletionPortThreads = {1}", minWorker, minCompletionPort);

			int maxWorker;
			int maxCompletionPort;
			ThreadPool.GetMaxThreads(out maxWorker, out maxCompletionPort);
			log.Info("Max WorkerThreads = {0}, Max CompletionPortThreads = {1}", maxWorker, maxCompletionPort);

			log.Info($"ThreadCount = {ThreadPool.ThreadCount}");
		}
	}
}
