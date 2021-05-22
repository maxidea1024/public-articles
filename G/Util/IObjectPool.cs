using System;

namespace G.Util
{
    /// <summary>
    /// Pool of objects.
    /// </summary>
    public interface IObjectPool<T> : IDisposable
    {
        /// <summary>
        /// Rent an object from pool.
        /// </summary>
        T Rent();

        /// <summary>
        /// Return an object to pool.
        /// </summary>
        void Return(T recycleable);
    }
}
