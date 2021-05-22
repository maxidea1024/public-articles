using System;

namespace G.Util
{
    //todo abstract로 구현하는게 좋으려나?

    /// <summary>
    /// An object that can be pooled in <c>IObjectPool</c>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRecycleable<T> : IDisposable
    {
        /// <summary>
        /// Set the action that will be invoked to return a rented object to the pool.
        /// </summary>
        void SetReturnToPoolAction(Action<T> returnToPoolAction);

        /// <summary>
        /// Reycle this object.
        /// </summary>
        void Return();

        /// <summary>
        /// Next reusable time.
        /// </summary>
        long NextReusableTime { get; set; }
    }
}
