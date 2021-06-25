using System;
using System.Collections.Generic;

namespace Prom.Core.Util
{
    // 구현해서 사용하는 경우가 많을 것이므로, 함수이름을 구체적으로 써야함.
    public interface IHoldingCounter
    {
        long IncreaseHoldingCount();
        long DecreaseHoldingCount();
        long HoldingCount { get; }
    }
}
