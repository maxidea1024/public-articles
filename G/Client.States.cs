
public ConnectionState ConnectionState
{
}

private void SetupStateMachine()
{
}

private void UpdateStateMachine()
{
    lock (_mainLock)
    {
        _stateMachine.Update();
    }
}


private void OnConnectingEnter()
{
    OnStart();
}

private void OnConnectingUpdate()
{
    if (_stateMachine.ElapsedTime > ClientOptions.ConnectionTimeout)
    {
        if (_logger != null)
        {
        }
        
        DisposeConnection(reason: RtStatusCodes.ConnectToServerTimedout,
                
    }
}

private void OnConnectingExit()
{
    // Pass
}


private void OnHandshakingEnter()
{
    _handshakeState = Handshake.WaitForServerHello;
    
    _keyPair = PublicKeyBox.GenerateKeyPair();
    _keyExchangeNonce = PublicKeyBox.GenerateNonce();

    var writer = new MessageWriter();
    writer.Write(RtCoreMessageType.ClientHello);
    writer.Write(ClientOptions.ProtocolVersion);
    writer.Write(NetworkConfig.CoreVersion);
    writer.Write(_keyPair.PublicKey);
    writer.Write(_keyExchangeNonce);
    _toServerConnection.SendNow(writer.PooledBuffer); //todo 이걸 어떻게 다루는게 좋을까?
}

private void OnHandshakingUpdate() {}
private void OnHandshakingExit() {}


private void OnConnectedEnter()
{
    if (IsPingingEnabled)
        StartPingTimer();
}

private void OnConnectedUpdate()
{
    if (_shutdownTcpRequestedTime != 0 &&
        (HeartbeatTime - _shutdownTcpRequestedTime) > _gracefuleDisconnectTimeout)
    {
        if (_logger != null)
            EnqueueTask(() => _logger.Log("A timeout occurred during graceful disconnecting processing. Switches to forced disconnect mode."));

        _shutdownTcpRequestedTime = 0;
        _gracefulDisconnectTimedoutHappended = true;

        NextConnectionState = ConnectionState.Disconnecting;
    }
    else
    {
        //LoopbackRecvCompletion();
    }
}

private void OnConnectedExit()
{
    if (IsPingingEnabled)
        StopPingTimer();
}


private void OnDisconnectingEnter()
{
    // 이건 할필요가 없지 않을까?
    if (IsPingingEnabled)
        StopPingTimer();

    _toServerConnection?.CloseSocketHandleOnly();

    //todo 이걸 여기서 처리하는게 맞는걸까?
    _groups.Clear();
    _peers.Clear();
}

private void OnDisconnectingExit()
{
    // Pass
}

private void OnDisconnectingUpdate()
{
    if (_toServerConnection == null)
    {
        ConnectionState = RtConnectionState.NotConnected;
        return;
    }
    
    bool hasPendingIos = _toServerConnection.HasPendingIos;
    int remainWorks = 0;
    lock (_userWorkQueueLock) //ConcurrentQueue로 구현하면 구지 락은 필요 없을듯 싶은데?
    {
        remainWorks = _userWorkQueue.Count;
    }

    if (!hasPendingIos && remainWorks == 0)
    {
        NextConnectionState = RtConnectionState.NotConnected;
        return;
    }
}


#region NotConnected state
private void OnNotConnectedEnter()
{
    //todo 이미 메인락이 잡힌 상태일테인데..

    lock (_mainLock)
    {
        if (_disconnectReason == RtStatusCodes.RequestedByUser &&
            (_gracefulDisconnectTimeout <= 0 || _gracefulDisconnectTimedoutHappended))
        {
            EnqueueLocalEvent(new LocalEvent
            {
                Type = LocalEventType.Disconnected,
                Status = new RtStatus
                {
                    StatusCode = _disconnectReason, //todo ClosingTicket으로 구현하는게 어떨려나??
                    Detail = "Disconnected",        //todo ClosingTicket으로 구현하는게 어떨려나?? 어쨌거나 저장해서 처리하는게 좋지 않을까?
                    RemoteID = LocalLinkId,
                }
            });
        }
    }

    OnStop();
}

private void OnNotConnectedExit() {}
private void OnNotConnectedUpdate() {}
#endregion




