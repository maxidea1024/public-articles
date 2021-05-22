using System;

namespace G.Util
{
	public class RefreshChecker
	{
		public TimeSpan Timeout { get; private set; }
		public DateTime RefreshedTime { get; private set; } = DateTime.MinValue;

		public RefreshChecker(int timeoutInSeconds)
		{
			Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
		}

		public RefreshChecker(TimeSpan timeout)
		{
			Timeout = timeout;
		}

		public bool Check(bool enforced = false)
		{
			DateTime now = DateTime.Now;

			var willRefresh = (enforced || (now - RefreshedTime) > Timeout);
			if (willRefresh) RefreshedTime = now;

			return willRefresh;
		}
	}
}
