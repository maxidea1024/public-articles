long clientLocalTime = RealtimeTime;

/*
var serverPong = new MessageOut();
serverPong.Write
*/

/*
var message = new OutgoingMessage();
*/

var writer = new MessageWriter();
writer.Write(RtCoreMessageType.ServerPong);
writer.Write(serverLocalTime);
writer.Write(clientLocalTime);
writer.Write(payload);
_toServerConnection.Send(writer);



internal void InitCoreProxyAndStubs()
{
    //todo 아래 메시지들을 구지 RPC로 구현해야했을까?
    //내 생각엔 구지 필요 없을듯 싶은데?
    //편하려고 하는걸까?
    _proxyC2S = new RealtimeEngine.ProxyC2S { InternalUseOnly = true };
    _stubS2C = new RealtimeEngine.StubS2C { InternalUseOnly = true };

    _stubS2C.GroupMemberJoined = Stub_GroupMemberJoined;
    _stubS2C.GroupMemberLeft = Stub_GroupMemberLeft;
    _stubS2C.RequestSelfClosingToClient = Stub_RequestSelfClosingToClient;
    _stubS2C.ShutdownTcpAck = Stub_ShutdownTckAck;

    BindProxy(_proxyC2S);
    BindStub(_stubS2C);
}

internal void Stub_GroupMemberJoined(LinkID remote, RtRpcContext context, int memberID, int groupID, int memberCount, byte[] userData)
{
    HandleGroupMemberJoinedEvent((LinkID)groupID, (LinkID)memberID, memberCount, userData);
}

internal void Stub_GroupMemberLeft(LinkID remote, RtRpcContext context, int memberID, int groupID, int memberCount)
{
    HandleGroupMemberLeftEvent((LinkID)memberID, (LinkID)groupID, memberCount);
}

//서버에서 클라에게 스스로 알아서 접속을 종료하라고 지시했음.
// context 객체는 풀링되는 녀석이니 참조를 캡쳐하면 안됨.
internal void StubRequestSoftClosingToClient(LinkId remoteId, RtRpcContext context)
{
    // 서버의 명령을 따르기위해서 접속 종료를 시작함.
    // 바로 종료하는것은 아니고 어느정도 절차가 수행됨.
    DisposeConnection(reason: RtStatusCodes.DisconnectFromRemote,
                    detail: "SoftClosing requested by server",
                    calledWhere: "StubRequestSoftClosingToClient");
}

internal void StubShotdownTcpAck(LinkId remoteId, RtRpcContext context)
{
    lock (_mainLock)
    {
        if (_shutdownTcpRequestedTime == 0 && _gracefulDisconnectTimeout > 0)
            _shutdownTcpRequestedTime = HeartbeatTime;
    }

    _proxyC2S.ShutdownTcpHandshake(LinkId.Server);
}

internal void ProcessCoreMssageServerPing(IncomingMessage message)
{
    long serverLocalTime;
    byte[] payload;
    try
    {
        var reader = new MessageReader(message.Body);
        reader.Read(out serverLocalTime);
        reader.Read(out payload);
        //todo 끝까지 다 읽었는지 체크해야하나?
    }
    catch (Exception e)
    {
        // 여러번 호출되었을 경우, 맨처음 사유만 이전해야함.
        // 최초로 접속을 끊으려고 했던 이유가 중요한거니까..
        DisposeConnection(reason: RtStatusCodes.MalformedMessageFormat,
            detail: e.ToString(),
            calledWhere: "ProcessCoreMssageServerPing");
        return;
    }
    
    long clientLocalTime = RealtimeTime;

    //todo 만약 버퍼 풀링을 했다면, 풀링된 버퍼를 넘겨주면 될듯한데..
    var serverPong = new PooledMessageWriter();
    serverPong.Write(RtCoreMessageType.ServerPong);
    serverPong.Write(serverLocalTime);
    serverPong.Write(clientLocalTime);
    serverPong.Write(payload);
    _toServerConnection.SendNow(serverPong.ToArray());
}

internal void ProcessCoreMessageRpc(IncomingMessage message)
{
    //todo 이 메시지를 처리된 후 참조를 놓아줘야함.
    EnqueueUserWork(new UserWorkItem(UserWorkItemType.RPC, message));
}
