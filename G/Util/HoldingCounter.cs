using System.Threading;

namespace G.Util
{
    /// <summary>
    /// Used when you want to hold the life of an object without locking.
    /// Since it is not possible to control the processing order,
    /// in a task where the order is important, hold the lock and process.
    /// </summary>
    public interface IHoldingCounter
    {
        long IncreaseHoldingCount();
        long DecreaseHoldingCount();
        long HoldingCount { get; }
    }

    public class HoldingCounter : IHoldingCounter
    {
        private long _count = 0;

        public long IncreaseHoldingCount()
        {
            return Interlocked.Increment(ref _count);
        }

        public long DecreaseHoldingCount()
        {
            return Interlocked.Decrement(ref _count);
        }

        public long HoldingCount => Interlocked.Read(ref _count);
    }
}
