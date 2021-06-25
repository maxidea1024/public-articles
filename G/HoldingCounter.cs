using System;
using System.Collections.Generic;

namespace Prom.Core.Util
{
    public class HoldingCounter : IHoldingCounter
    {
        private long _counter = 0;

        public long IncreaseHoldingCount() => Interlocked.Increment(ref _count);
        public long DecreaseHoldingCount() => Interlocked.Decrement(ref _count);
        public long HoldingCount => Interlocked.Read(ref _count);
    }
}
