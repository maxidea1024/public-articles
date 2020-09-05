//todo unsafe ref to treat it as a pointer?

using System;

/// <summary>
/// Zero copy byte queue
///
/// It is not a zero copy when accessed through the Enqueue / Dequeue function, but
/// When accessing through EnqueueNoCopy / DeqeueNoCopy function, zero copy
/// can be achieved. When used for asynchronous I/O operations, additional copy costs can be eliminated.
/// </summary>
public class ZeroCopyByteQueue
{
    /// <summary>
    /// The maximum length of the queue in bytes
    /// </summary>
    public int Capacity => _bufferLength;

    /// <summary>
    /// Length of queued data in bytes
    /// </summary>
    public int Count => _usedLength;

    /// <summary>
    /// Memory buffer
    /// </summary>
    public byte[] Buffer => _buffer;

    /// <summary>
    /// Start position of span where data can be writes linearly
    /// </summary>
    public int WritableSpanPosition => _rearPosition;

    /// <summary>
    /// The length of the span in which data can be writable linearly in bytes
    /// </summary>
    public int WritableSpanLength
    {
        get
        {
            // Of the length that can be used in two divided spans and the length that can be used in one span
            // Choose a smaller length. That is, it takes the length of the span that can be accessed linearly.
            int writableSpanLengthWhenSplitted = _bufferLength - _rearPosition;
            int writableSpanLengthWhenLinear = _bufferLength - _usedLength;
            return Math.Min(writableSpanLengthWhenSplitted, writableSpanLengthWhenLinear);
        }
    }

    /// <summary>
    /// Start position of span where data can be reads linearly
    /// </summary>
    public int ReadableSpanPosition => _frontPosition;

    /// <summary>
    /// The length of the span in which data can be readable linearly in bytes
    /// </summary>
    public int ReadableSpanLength
    {
        get
        {
            if ((_frontPosition + _usedLength) > _bufferLength)
            {
                return _bufferLength - _frontPosition;
            }

            return _usedLength;
        }
    }

    /// <summary>
    /// Returns true if the queue is empty
    /// </summary>
    public bool IsEmpty => _usedLength == 0;

    /// <summary>
    /// Returns true if the queue is full
    /// </summary>
    public bool IsFull => _usedLength >= _bufferLength;

    private byte[] _buffer;
    private int _usedLength;
    private int _growBy;
    private int _bufferLength;
    private int _frontPosition;
    private int _rearPosition;

    /// <summary>
    /// Initialize with queue buffer length and growth length.
    /// </summary>
    /// <param name="bufferLength">Queue buffer length(in bytes)</param>
    /// <param name="growBy">Queue growth step(in bytes)</param>
    public ZeroCopyByteQueue(int bufferLength, int growBy = 1024)
    {
        _growBy = growBy;
        _bufferLength = bufferLength;
        _buffer = new byte[_bufferLength];
        _frontPosition = 0;
        _rearPosition = 0;
        _usedLength = 0;
    }

    /// <summary>
    /// Put the data on the queue. This function is not zero-copy and should be avoided if possible.
    /// </summary>
    public bool Enqueue(byte[] data, int offset, int length)
    {
        if ((_usedLength + length) > _bufferLength)
        {
            Grow(length);
            return false;
        }

        if ((_rearPosition + length) > _bufferLength)
        {
            // Splitted

            int span1Length = _bufferLength - _rearPosition;
            int span2Length = (_rearPosition + length) - _bufferLength;

            Array.Copy(data, offset, _buffer, _rearPosition, span1Length);
            Array.Copy(data, offset + span1Length, _buffer, 0, span2Length);
        }
        else
        {
            // Linear

            Array.Copy(data, offset, _buffer, _rearPosition, length);
        }

        _rearPosition += length;
        if (_rearPosition >= _bufferLength)
        {
            _rearPosition -= _bufferLength;
        }

        _usedLength = length;

        return true;
    }

    /// <summary>
    /// If data is filled in the queue in asynchronous I/O, etc., zero-copy can be achieved by processing
    /// as if it was queued as much as it was filled.
    /// This is the function used in this case.
    /// </summary>
    public bool EnqueueNoCopy(int length)
    {
        if ((_usedLength + length) > _bufferLength)
        {
            return false;
        }

        _rearPosition += length;
        if (_rearPosition >= _bufferLength)
        {
            _rearPosition -= _bufferLength;
        }

        _usedLength += length;

        return true;
    }

    /// <summary>
    /// Fetch data from the queue. It is not a zero-copy, so it is a function that is no different from a normal queue implementation.
    /// </summary>
    public bool Dequeue(byte[] buffer, int offset, int length)
    {
        if (length > _usedLength)
        {
            return false;
        }

        if ((_frontPosition + length) > _bufferLength)
        {
            // Splitted

            int span1Length = _bufferLength - _frontPosition;
            int span2Length = (_frontPosition + length) - _bufferLength;

            Array.Copy(_buffer, _frontPosition, buffer, offset, span1Length);
            Array.Copy(_buffer, 0, buffer, offset + span1Length, span2Length);
        }
        else
        {
            // Linear

            Array.Copy(_buffer, _frontPosition, buffer, offset, length);
        }

        _frontPosition += length;
        if (_frontPosition >= _bufferLength)
        {
            _frontPosition -= _bufferLength;
        }

        _usedLength -= length;

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
        if (_frontPosition >= _bufferLength)
        {
            _frontPosition -= _bufferLength;
        }

        _usedLength -= length;

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

    private void Grow(int lengthHint)
    {
        int newBufferLength = (_usedLength + lengthHint + _growBy);
        byte[] newBuffer = new byte[newBufferLength];
        Dequeue(newBuffer, 0, _usedLength);

        _frontPosition = 0;
        _rearPosition = _usedLength;

        _bufferLength = newBufferLength;
        _buffer = newBuffer;
    }

    public void Shrink()
    {
        //todo
    }
}
