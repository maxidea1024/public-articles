
//todo
//함수 람다로 묶어서 전달한다면, 서버/클라 이벤트 처리 주체를
// 능동적으로 선택할 수 있을듯 싶은데..
async Task RemoteClient.RunToProcessAsync()
{
    while (!_cts.IsCancellationRequested)
    {
        if (_requests.Count == 0)
            await Task.Yield();

        // 딜레이가 거의 없다.
        var requests = _requests.GetAll(); // take all and swaps.
        while (requests.Count > 0)
        {
            // 내부 요청처리시에 _c
            var request = requests.Dequeue();
            try
            {
                _cts.ThrowExceptionIfCancellationRequested();

                await request(_cts.Token);
            }
            catch (CancellationException e)
            {
                // 의도적으로 취소한 경우이므로..
                break;
            }
            catch (Exception e)
            {
                // Recallback?
            }
        }
    }
}





using System;
using System.Collections.Generic;

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
