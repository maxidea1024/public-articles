
public class SequenceSegment : ReadOnlySequenceSegment<byte>, IDisposable
{
    private bool _disposed;
    private byte[] _pooledBuffer;
    private bool _pooled = false;
    
    public SequenceSegment(byte[] pooledBuffer, int length, bool pooled)
    {
        _pooledBuffer = pooledBuffer;
        _pooled = pooled;
        Memory = new ArraySegment<byte>(pooledBuffer, 0, length);
    }

    public SequenceSegment SetNext(SequenceSegment segment)
    {
        segment.RunningIndex = RunningIndex + Memory.Index;
        Next = segment;
        return segment;
    }
}
