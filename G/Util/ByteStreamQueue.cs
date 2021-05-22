using System;
using G.Util;

namespace G.Util
{
    /// <summary>
    /// Zero copyable byte stream queue.
    ///
    /// It is not a zero copy when accessed through the Enqueue / Dequeue function, but
    /// When accessing through EnqueueNoCopy / DeqeueNoCopy function, zero copy
    /// can be achieved. When used for asynchronous I/O operations, additional copy costs can be eliminated.
    /// </summary>
    public class ByteStreamQueue
    {
        /// <summary>The maximum length of the queue in bytes</summary>
        public int Capacity => _capacity;

        /// <summary>Length of queued data in bytes</summary>
        public int Count => _usedLength;

        /// <summary>Memory buffer</summary>
        public byte[] Buffer => _buffer;

        /// <summary>Writable memory span</summary>
        public Memory<byte> WritableMemory => new Memory<byte>(_buffer, WritableSpanPosition, WritableSpanLength);

        /// <summary>Start position of span where data can be writes linearly</summary>
        public int WritableSpanPosition => _rearPosition;

        /// <summary>The length of the span in which data can be writable linearly in bytes</summary>
        public int WritableSpanLength
        {
            get
            {
                // Of the length that can be used in two divided spans and the length that can be used in one span
                // Choose a smaller length. That is, it takes the length of the span that can be accessed linearly.
                int writableSpanLengthWhenSplitted = _capacity - _rearPosition;
                int writableSpanLengthWhenLinear = _capacity - _usedLength;
                return Math.Min(writableSpanLengthWhenSplitted, writableSpanLengthWhenLinear);
            }
        }

        /// <summary>Readable memory span</summary>
        public ReadOnlyMemory<byte> ReadableMemory => new ReadOnlyMemory<byte>(_buffer, ReadableSpanPosition, ReadableSpanLength);

        /// <summary>Start position of span where data can be reads linearly</summary>
        public int ReadableSpanPosition => _frontPosition;

        /// <summary>The length of the span in which data can be readable linearly in bytes</summary>
        public int ReadableSpanLength
        {
            get
            {
                if ((_frontPosition + _usedLength) > _capacity)
                {
                    return _capacity - _frontPosition;
                }

                return _usedLength;
            }
        }

        /// <summary>Returns true if the queue is empty</summary>
        public bool IsEmpty => _usedLength == 0;

        /// <summary>Returns true if the queue is full</summary>
        public bool IsFull => _usedLength >= _capacity;

        private byte[] _buffer;
        private int _capacity;
        private readonly int _growBy;

        private int _usedLength;
        private int _frontPosition;
        private int _rearPosition;

        /// <summary>
        /// Initialize with queue buffer length and growth length.
        /// </summary>
        /// <param name="bufferLength">Queue buffer length(in bytes)</param>
        /// <param name="growBy">Queue growth step(in bytes)</param>
        public ByteStreamQueue(int bufferLength, int growBy = 1024)
        {
            _growBy = growBy;
            _capacity = bufferLength;
            _buffer = new byte[_capacity];  //todo 버퍼를 미리 잡지 않는다면 효율을 높일 수 있을듯..
            _frontPosition = 0;
            _rearPosition = 0;
            _usedLength = 0;
        }

        public bool Enqueue(ArraySegment<byte> data)
        {
            return Enqueue(data.Array, data.Offset, data.Count);
        }

        /// <summary>
        /// Put the data on the queue. This function is not zero-copy and should be avoided if possible.
        /// </summary>
        public bool Enqueue(byte[] data)
        {
            PreValidations.CheckNotNull(data);
            return Enqueue(data, 0, data.Length);
        }

        /// <summary>
        /// Put the data on the queue. This function is not zero-copy and should be avoided if possible.
        /// </summary>
        public bool Enqueue(byte[] data, int length)
        {
            PreValidations.CheckNotNull(data);
            return Enqueue(data, 0, length);
        }

        /// <summary>
        /// Put the data on the queue. This function is not zero-copy and should be avoided if possible.
        /// </summary>
        public bool Enqueue(byte[] data, int offset, int length)
        {
            if (length > 0)
            {
                PreValidations.CheckNotNull(data);
            }

            if ((_usedLength + length) > _capacity)
            {
                Grow(length);
            }

            if ((_rearPosition + length) > _capacity)
            {
                // Splitted

                int span1Length = _capacity - _rearPosition;
                int span2Length = (_rearPosition + length) - _capacity;

                Array.Copy(data, offset, _buffer, _rearPosition, span1Length);
                Array.Copy(data, offset + span1Length, _buffer, 0, span2Length);
            }
            else
            {
                // Linear

                Array.Copy(data, offset, _buffer, _rearPosition, length);
            }

            _rearPosition += length;
            if (_rearPosition >= _capacity)
            {
                _rearPosition -= _capacity;
            }

            _usedLength += length;

            return true;
        }

        /// <summary>
        /// If data is filled in the queue in asynchronous I/O, etc., zero-copy can be achieved by processing
        /// as if it was queued as much as it was filled.
        /// This is the function used in this case.
        /// </summary>
        public bool EnqueueNoCopy(int length)
        {
            if ((_usedLength + length) > _capacity)
            {
                return false;
            }

            _rearPosition += length;
            if (_rearPosition >= _capacity)
            {
                _rearPosition -= _capacity;
            }

            _usedLength += length;

            return true;
        }

        /// <summary>
        /// Fetch data from the queue. It is not a zero-copy, so it is a function that is no different from a normal queue implementation.
        /// </summary>
        public bool Dequeue(byte[] buffer)
        {
            PreValidations.CheckNotNull(buffer);
            return Dequeue(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Fetch data from the queue. It is not a zero-copy, so it is a function that is no different from a normal queue implementation.
        /// </summary>
        public bool Dequeue(byte[] buffer, int length)
        {
            return Dequeue(buffer, 0, length);
        }

        /// <summary>
        /// Fetch data from the queue. It is not a zero-copy, so it is a function that is no different from a normal queue implementation.
        /// </summary>
        public bool Dequeue(byte[] buffer, int offset, int length)
        {
            if (length > 0)
            {
                PreValidations.CheckNotNull(buffer);
            }

            if (length > _usedLength)
            {
                return false;
            }

            if ((_frontPosition + length) > _capacity)
            {
                // Splitted

                int span1Length = _capacity - _frontPosition;
                int span2Length = (_frontPosition + length) - _capacity;

                Array.Copy(_buffer, _frontPosition, buffer, offset, span1Length);
                Array.Copy(_buffer, 0, buffer, offset + span1Length, span2Length);
            }
            else
            {
                // Linear

                Array.Copy(_buffer, _frontPosition, buffer, offset, length);
            }

            _frontPosition += length;
            if (_frontPosition >= _capacity)
            {
                _frontPosition -= _capacity;
            }

            _usedLength -= length;

            ShrinkIfNeeded();

            return true;
        }

        /// <summary>
        /// This is a function that pulls data out of the queue without the cost of copying.
        /// (Actually, it does not pull the data and only adjusts the offset.)
        /// You can achieve zero-copy through this function.
        /// </summary>
        public bool DequeueNoCopy(int length)
        {
            if (length > _usedLength)
            {
                return false;
            }

            _frontPosition += length;
            if (_frontPosition >= _capacity)
            {
                _frontPosition -= _capacity;
            }

            _usedLength -= length;

            ShrinkIfNeeded();

            return true;
        }

        /// <summary>
        /// Peek some data from qeueu without modifying pointers.
        /// </summary>
        public bool Peek(byte[] buffer)
        {
            PreValidations.CheckNotNull(buffer);
            return Peek(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Peek some data from qeueu without modifying pointers.
        /// </summary>
        public bool Peek(byte[] buffer, int length)
        {
            return Peek(buffer, 0, length);
        }

        /// <summary>
        /// Peek some data from qeueu without modifying pointers.
        /// </summary>
        public bool Peek(byte[] buffer, int offset, int length)
        {
            if (length > _usedLength)
            {
                return false;
            }

            if ((_frontPosition + length) > _capacity)
            {
                // Splitted

                int span1Length = _capacity - _frontPosition;
                int span2Length = (_frontPosition + length) - _capacity;

                Array.Copy(_buffer, _frontPosition, buffer, offset, span1Length);
                Array.Copy(_buffer, 0, buffer, offset + span1Length, span2Length);
            }
            else
            {
                // Linear

                Array.Copy(_buffer, _frontPosition, buffer, offset, length);
            }

            return true;
        }

        /// <summary>
        /// Empty all contents in the queue.
        /// </summary>
        public void Clear()
        {
            _frontPosition = 0;
            _rearPosition = 0;
            _usedLength = 0;
        }

        /// <summary>
        /// Grow memory buffer.
        /// </summary>
        private void Grow(int lengthHint)
        {
            //todo 지수형태로 늘어나는게 바람직해보임
            //     확복된 메모리 대비 일정수준 이하로 사용중일 경우에는 메모리 버퍼를 축소함.
            //     내부 버퍼를 풀링해야할까?

            //todo enlarge 매커니즘을 어떻게 적용해야할까?
            //todo 확장되는 크기를 어떤식으로 처리하는게 바람직할까?

            int newBufferLength = (_usedLength + lengthHint + _growBy); //fixme

            byte[] newBuffer = new byte[newBufferLength];
            Dequeue(newBuffer, 0, _usedLength);

            _frontPosition = 0;
            _rearPosition = _usedLength;

            _capacity = newBufferLength;
            _buffer = newBuffer;
        }

        private void ShrinkIfNeeded()
        {
            //todo
        }

        public void Shrink()
        {
            //todo
        }
    }
}
