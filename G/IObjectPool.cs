using System;
using System.Collections.Generic;

namespace Prom.Core.Util
{
    public interface IObjectPool<T> : IDisposable
    {
        T Rent();

        void Return(T recycleable);
    }
}
