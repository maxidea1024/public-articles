
private void CloseConnection(int reason, string detail = "", SocketError socketError = SocketError.Success, string calledWhere = "")
{
    EnqueueConnectFailureEvent(reason, detail, socketError, calledWhere);

    //todo interlocked로 처리하자.
    NextConnectionState = ConnectionState.Disconnecting;
}

private void EnqueueConnectFailureEvent(int reason, string detail = "", SocketError socketError = SocketError.Success, string calledWhere = "")
{
    lock (_mainLock)
    {
        // 이미 호출되었음.
        // interlocked로 처리하는게 좋을듯한데..
        if (_disposeCaller != null)
        {
            EnqueueTask(() => _logger?.Log("EnqueueConnectionFailureEvent(): _disposeCaller != null");
            return;
        }

        _disposeCaller = calledWhere;
    }

    if (_logger != null)
    {
        EnqueueTask(() => _logger?.Log("EnqueueConnectFailureEvent() is called."));
    }

    var connectLocalEvent = new ConnectLocalEvent
    {
        Type = LocalEventType.ConnectToServerFailure,
        Status = new RtStatus
        {
            StatusCode = reason,
            Detail = detail,
            SocketError = socketError,
            RemoteId = LinkId.Server
        },
        RemoteAddress = new IPEndPoint(_serverEndPoint.Address, _serverEndPoint.Port) // 미리 만들어둘수 있을듯 싶은데..
    }
}

private void EnqueueConnectFailureEvent(int reason, RtStatus status)
{
    EnqueueLocalEvent(new ConnectLocalEvent
    {
        Type = LocalEventType.ConnectToServerFailure,
        Status = status
    });
}

// 블럭킹 모드로 연결 끊기.
public bool Disconnect(RtDisconnectOptions disconnectOptions = null)
{
    // 이미 끊고 있는중이면, 성공으로 간주하고 바로 리턴.
    if (ConnectionState > ConnectionState.Connected)
        return true;

    lock (_mainLock)
    {
        if (_disconnectAsyncCalled)
            return false;
    }

    //그냥 Wait() 하면 되는거 아닌가?
    DisconnectAsync(disconnectOptions).ConfigureAwait(false).GetAwaiter().GetResult();

    return true;
}

public async Task<bool> DisconnectAsync(RtDisconnectOptions disconnectOptions = null)
{
    // 이미 끊고 있는중이면, 성공으로 간주하고 바로 리턴.
    if (ConnectionState > ConnectionState.Connected)
        return true;
    
    lock (_mainLock)
    {
        if (_disconnectAsyncCalled)
            return false;
    }
}

private void OnConnectionDisconnected(TcpConnection connection, TransportException e)
{
}

private void OnConnectionDataReceived(TcpConnection connection, int bytesReceived)
{
}

internal override void EnqueueWarning(RtStatus status)
{
}

public bool SetLinkTag(LinkId linkId, object tag)
{
    lock (_mainLock)
    {
        if (linkId == LinkId.None)
            return false;

        // Local?
        if (linkId == _localLinkId)
        {
            _localLinkTag = tag;
            return true;
        }
        
        // Server?
        if (linkId == LinkId.Server)
        {
            _serverLinkTag = tag;
            return true;
        }
        
        // Peer?
        var peer = GetPeer(linkId);
        if (peer != null)
        {
            peer.LinkTag = tag;
            return true;
        }

        // Group?
        var group = GetGroup(linkId);
        if (group != null)
        {
            group.LinkTag = tag;
            return true;
        }

        return false;
    }
}

private void OnStart()
{
    if (_logger != null)
    {
        //실행되고 있는 와중에 _logger가 null이면 어찌되는거임?
        EnqueueTask(() => _logger.Log("OnStart() is called.");
    }

    _statCounters.Reset();
    
    StartHeartbeatTimer();
    
    _toServerTcp.Connect(_serverEndPoint);
}

void OnStop()
{
    if (_logger != null)
    {
        EnqueueTask(() => _logger.Log("OnStop() is called.");
    }
    
    StopHeartbeatTimer();
    FlushUserWorkQueue();
    UnsafeCleanup();
}



