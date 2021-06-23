using System;
using System.Collections.Generic;

namespace Prom.Core.Collections
{
    /// <summary>
    /// Double buffered generic queue collection.
    /// </summary>
    public class DoubleBufferedQueue<T>
    {
        // Readable queue
        private Queue<T> _readableQueue;
        // Writable queue
        private Queue<T> _writableQueue;

        // Internal queue1
        private readonly Queue<T> _queue1 = new Queue<T>();
        // Internal queue2
        private readonly Queue<T> _queue2 = new Queue<T>();

        // Locking object for swapping.
        private readonly object _swapLock = new object();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DoubleBufferedQueue()
        {
            _readableQueue = _queue1;
            _writableQueue = _queue2;
        }

        /// <summary>
        /// Enqueue an item into writable queue.
        /// </summary>
        public void Enqueue(T item)
        {
            lock (_swapLock)
            {
                _writableQueue.Enqueue(item);
            }
        }

        /// <summary>
        /// Get readable queue and swap internal queues.
        /// </summary>
        public Queue<T> GetAll()
        {
            SwapQueues();
            return _readableQueue;
        }

        /// <summary>
        /// Swaps readable and writable queues.
        /// </summary>
        private void SwapQueues()
        {
            lock (_swapLock)
            {
                var tmp = _readableQueue;
                _readableQueue = _writableQueue;
                _writableQueue = tmp;
            }
        }
    }
}
