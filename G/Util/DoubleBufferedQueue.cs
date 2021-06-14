using System;
using System.Collections.Generic;

namespace G.Util
{
    /// <summary>
    /// Double buffered generic queue collection.
    /// </summary>
    public class DoubleBufferedQueue<T>// where T: class
    {
        private readonly Queue<T> _queue1 = new Queue<T>();
        private readonly Queue<T> _queue2 = new Queue<T>();

        private Queue<T> _input;
        private Queue<T> _output;

        private readonly object _lock = new object();

        /// <summary>
        /// Default constructor
        /// </summary>
        public DoubleBufferedQueue()
        {
            _input = _queue1;
            _output = _queue2;
        }

        /// <summary>
        /// Enqueue an item into input queue.
        /// </summary>
        public void Enqueue(T item)
        {
            lock (_lock)
            {
                _input.Enqueue(item);
            }
        }

        /// <summary>
        /// Get output queue and swap internal queues.
        /// </summary>
        public Queue<T> GetAll()
        {
            SwapQueues();

            return _output;
        }

        /// <summary>
        /// Swaps input and output queues.
        /// </summary>
        private void SwapQueues()
        {
            lock (_lock)
            {
                var tmp = _input;
                _input = _output;
                _output = tmp;
            }
        }
    }
}
