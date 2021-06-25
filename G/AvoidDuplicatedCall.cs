
/// <summary>
/// 세션이 닫힐때 호출되는 콜백
/// </summary>
protected virtual void OnClosed()
{
    // 여러번 콜백이 호출되는것을 방지하기 위해서 사용함.

    IsClosed = true;

    // 이미 null이거나 다른곳에서 변경한 경우에는 수행하지 않음.
    var closed = Closed;
    if (closed == null || Interlocked.CompareExchange(ref Closed, null, closed) != closed)
        return;

    // closed 콜백 호출시 close 사유도 같이 전달해줌.
    var closeReason = CloseReason.HasValue ? CloseReason.Value : Channel.CloseReason.Unknown;
    closed.Invoke(this, new CloseEventArgs(closeReason));
}

/// <summary>
/// 채널을 닫아줌.
/// </summary>
protected override void TcpChannel.Close()
{
    var socket = _socket;

    // 이미 소켓을 닫아서 null이거나 다른곳에서 변경한 경우에는 수행하지 않음.
    if (socket == null || Interlocked.CompareExchange(ref _socket, null, socket) != socket)
        return;

    try
    {
        socket.Shutdown(SocketShutdown.Both);
    }
    finally
    {
        socket.Close();
    }
}

/// <summary>
/// 무시 가능한 소켓 오류인가?
/// </summary>
protected override bool TcpChannel.IsIgnorableException(Exception e)
{
    if (base.IsIgnorableException(e))
        return true;

    if (e is SocketException se)
    {
        if (se.IsIgnoableSocketException())
            return true;
    }

    return false;
}

protected override async ValueTask<int> SendOverIoAsync(ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
{
    if (buffer.IsSingleSegment)
    {
        return await _socket
            .SendAsync(GetArrayByMemory(buffer.First), SocketFlags.None, cancellationToken)
            .ConfigureAwait(false);
    }

    if (_segmentsForSend == null)
        _segmentsForSend = new List<ArragySegment<byte>>();
    else
        _segmentsForSend.Clear();

    var segments = _segmentsForSend;

    foreach (var piece in buffer)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _segmentsForSend.Add(GetArrayByMemory(piece));
    }

    cancellationToken.ThrowIfCancellationRequested();

    return await _socket
                .SendAsync(_segmentsForSend, SocketFlags.None)
                .ConfigureAwait(false);
}

private async ValueTask<int> ReceiveAsync(Socket socket, Memory<byte> memory, SocketFlags socketFlags, CancellationToken cancellationToken)
{
}


public class SessionHandlers
{
    public Func<IAppSession, ValueTask> Connected { get; set; }

    public Func<IAppSession, CloseEventArgs, ValueTask> Closed { get; set; }
}


public class IdleSessionPurger
{
    private ISessionContainer _sessionContainer;
    private Timer _timer;

    private void OnTimerCallback(object state)
    {
        // 재진입방지
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        try
        {
            var timeoutTime = DateTimeOffset.Now.AddSeconds(0 - _serverOptions.IdleSessionTimeOut);
            
            foreach (var session in _sessionContainer.GetSessions())
            {
                if (s.LastActiveTime <= timeoutTime)
                {
                    try
                    {
                        _ = session.Channel.CloseAsync(CloseReason.TimedOut);
                        _logger?.LogWarning($"Close the idle session {s.SessionId}, it's LastActiveTime is {s.LastActiveTime}.");
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError(e, $"Error happened when close the session {s.SessionId} for inactive for a while.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error happened when clear idle session.");
        }

        // 타이머 재개
        _timer.Change(_serverOptions.ClearIdleSessionInterval * 1000, _serverOptions.ClearIdleSessionInterval * 1000);
    }
}

