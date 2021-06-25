
internal class RemoteClient : ListNode<RemoteClient>, IHoldingCounter
{
    public LinkId LinkId { get; set; }
    public object LinkTag { get; set; }
    public bool IsAuthed => LinkId != LinkId.None;
    public IPEndPoint ExternalAddress { get; set; }
    public IPEndPoint InternalAddress { get; set; }
    public List<LinkId> JoinedGroups { get; private set; }
    public long CreatedTime { get; private set; }
    public TcpConnection ToClientConnection { get; private set; }

    #region HoldingCounter
    private long _holdingCounter = 0;

    public long IncreaseHoldingCount()
    {
        return Interlocked.Increment(ref _holdingCounter);
    }

    public long DecreaseHoldingCount()
    {
        return Interlocked.Decrement(ref _holdingCounter);
    }

    public long HoldingCount => Interlocked.Read(ref _holdingCounter);
    #endregion

    #region Encryption
    internal KeyPair _keyPair;
    internal byte[] _clientPublicKey;
    internal byte[] _keyExchangeNonce;
    internal CryptoKey _sessionKeyAes128;
    internal CryptoKey _sessionKeyChaCha20;
    #endregion

    public ClosingTicket _closingTicket { get; set; }

    // CloseClient시에 클라이언트에게 `RequestClientSelfCloing` 메시지를 보낸 시각
    // 일정 시간이 지난 후에는 강제 연결 해제 절차를 수행함.
    public long _softClosingRequestedTime { get; set; }

    // 후보에서 오래 머무른 연결을 해제하기 위한 용도로 사용됨.
    public bool _purgeRequested { get; set; }

    // 클라이언트에서 ShutdownTcp 메시지와 함께 넘어온 작별 인사.
    public byte[] _shutdownComment { get; set; }

    // 디버깅 용도로 사용되며, 중복 Dispose 호출을 막기 위한 용도와 어디서 Dispose가 호출되었는지를 알아내는 용도로 사용됨.
    public string _disposeCaller { get; set; }

    //맨처음 값을 설정해주던지
    //0으로 해놓고 0인 경우에는 받은적이 없던걸로 하던지..
    //실질적으로는 설정은 해주는게 좋겠다.
    //어짜피 끊어내기 위한 시간을 측정하기 위함이니까..
    public long _lastTcpStreamReceivedTime { get; set; }

    // Current handshake state
    public HandshakeState _handshakeState { get; set; }

    public RemoteClient(RtServer server, TcpConnection connection)
    {
        _server = server;
        _linkId = LinkId.None;
        _joinedGroups = new List<LinkId>();
        _createdTime = server.HeartbeatTime;
        _toClientConnection = connection;
        _lastTcpStreamReceivedTime = _server.HeartbeatTime;
    }

    public RtRemoteClientInfo GetInfo()
    {
        return new RtRemoteClientInfo
        {
            LinkId = _linkId,
            ExternalAddress = _externalAddress,
            InternalAddress = _internalAddress,
            JoinedGroups = _joinedGroups,
            LinkTag = _linkTag,

        };
    }

    public void HardClose(int reason,
            string detail = "",
            byte[] comment = null,
            string calledWhere = null,
            SocketError socketError = SocketError.Success)
    {
        // It doesn't matter if you call it multiple times.
        _server.HardCloseClient(this, reason, detail, comment, calledWhere, socketError);
    }

    public bool IsSendIssued
    {
        get => _server.IsSendIssued(this);
    }

    public void SendMessage(OutgoingMessage message)
    {
        //todo 너무 많이 쌓아였으면 여기서 플러쉬를 해주도록 하자.
        //todo 너무 많이 쌓아였으면 여기서 플러쉬를 해주도록 하자.
        //todo 너무 많이 쌓아였으면 여기서 플러쉬를 해주도록 하자.
        //todo 너무 많이 쌓아였으면 여기서 플러쉬를 해주도록 하자.

        _toClientConnection.SendMessage(message);

        _server?.RegisterScheduledSendMessage(this);
    }

    public void SendMessageNonScheduled(OutgoingMessage message)
    {
        _server?.UnregisterScheduledSendMessage(this);

        _toClientConnection.SendMessageNow(message);
    }
}
