
public bool JoinIntoGroup(LinkId groupId, LinkId memberId, byte[] comment = null)
{
    comment ??= Array.Empty<byte>();

    lock (_mainLock)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return false;

        var member = GetClient(memberId);
        if (member == null)
            return flase;

        if (!group.Join(member))
            return false;

        foreach (var m in group.Members)
            _proxyS2C.GroupMemberJoined(m, (int)memberId, (int)groupId, group.MemberCount, comment);

        foreach (var m in group.Members)
        {
            if (m != memberId)
                _proxyS2C.GroupMemberJoined(memberId, (int)m, (int)groupId, group.MemberCount, userData);
        }

        return true;
    }
}

public bool LeaveGroup(LinkId groupId, LinkId memberId)
{
    lock (_mainLock)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return false;

        var member = GetClient(memberId);
        if (member == null)
            return flase;

        if (!group.Leave(member))
            return false;

        foreach (var m in group.Members)
            _proxyS2C.GroupMemberLeft(m, (int)memberID, (int)groupID, group.Members.Count);

        if (ServerOptions.AllowEmptyGroups && group.MemberCount == 0)
            _groups.Remove(groupId);

        return true;
    }
}

#region Listening
private void StartListening()
{
    if (ServerOptions.UseDynamicListeningPort)
    {
        var listener = TcpListener();
        _listeners.Add(listener);

        //IPv4, IPv6는 어떻게 지정해야하나?
        //일단 IP Listeral은 있어야하는듯..
        listener.StartListening(ServerOptions.ServerIp, 0); // 0=dynamic port
    }
    else
    {
        foreach (var listeningPort in ServerOptions.ListeningPorts)
        {
            var listener = TcpListener();
            _listeners.Add(listener);

            listener.NewConnection += OnNewConnection;
            listener.StartListening(ServerOptions.ServerIp, listeningPort);
        }
    }
}

private void StopListening()
{
    lock (_listeningLock)
    {
        foreach (var listener in _listeners)
        {
            listener.CloseSocketHandleOnly();
        }

        _listeners.Clear();
    }
}
#endregion



// 특정 순간에만 유효함.
public bool IsValidClient(LinkId clientId)
{
    return GetClient(clientId) != null;
}

public int ClientCount
{
    get
    {
        //todo ConcurrentDictionary로 변경하면 명시적인 락은 피해갈 수 있지 않을까?
        lock (_clientsLock)
            return _clients.Count;
    }
}

public int CandidateCount
{
    get
    {
        lock (_candidatesLock)
            return _candidates.Count;
    }
}

public int SuspendedCount
{
    get
    {
        lock (_suspendedsLock)
            return _suspendeds.Count;
    }
}

public void CloseClients()
{
    lock (_clientsLock)
    {
        foreach (var pair in _clients)
        {
            var client = pair.Value;

            if (client.ClosingTicket == null)
            {
                RequestSoftClosingToClient(client);
            }
        }
    }
}

public void CloseClient(LinkId clientId)
{
    lock (_clientsLock)
    {
        var client = GetClient(clientId);
        if (client == null)
            return;

        _logger?.Log($"Call CloseConnection(clientId:{clientId})");

        RequestSoftClosingToClient(client);
    }
}

// 락을 걸고 들어온다.
private RequestSoftClosingToClient(RemoteClient client)
{
    // 이미 요청한 경우에는 바로 건너뛴다.
    if (client.SelfClosingRequestedTime != 0)
        return;

    // 중복 요청을 막기위해서 요청 시각을 기록해둠.
    client.SelfClosingRequestedTime = HeartbeatTime;

    _logger?.Log($"Call RequestSoftClosingToClient({client.LinkId})");

    // 클라에게 스스로 닫을 수 있도록 지령한다.
    _proxyS2C.RequestSoftClosingToClient(client.LinkId, RtRpcCallOptions.ReliableCoreOnly);
}

private void SwitchToHardClosingModeIfSoftClosingGoesTooLongClient(RemoteClient client)
{
    if (client.SelfClosingRequestedTime == 0)
        return;

    if ((HeartbeatTime - client.SelfClosingRequestedTime) < NetworkConfig.ClientSoftClosingTimeout)
        return;

    _logger?.Log($"Client {client.RemoteId} is asked to disconnect by itself, but the connection is not disconnected for {NetworkConfig.ClientSoftClosingTimeout}ms, so it switches to forced disconnect mode.");

    // Soft-closing 모든 해제함.
    client.SelfClosingRequestedTime = 0;

    // Hard-closing 모드로 전환한다.
    // 이 모드로 전환하면 되돌릴 수 없음.
    HardCloseClient(client,
            reason: RtStatusCodes.DisconnectFromLocal,
            detail: "",
            comment: null,
            calledWhere: "SwitchToHardClosingModeIfSoftClosingGoesTooLongClient");
}

private void PurgeHardClosingRequestedClients()
{
    lock (_clientsLock)
    {
        if (_hardClosingRequestedClients.Count ==0)
            return;

        List<RemoteClient> garbages = null;
        foreach (var pair in _hardClosingRequestedClients)
        {
            var client = pair.Key;
            var requestedTime = pair.Value; //todo client내부에 HardClosingRequestedTime을 변수로 넣어주자.

            bool isHeld = client.HoldingCount > 0;
            bool isSending = client.Transport.IsSending;
            bool isReceiving = client.Transport.IsReceiving;
            bool hasPendingIOs = isSending || isReceiving;
            bool hasPendingTasks = client.TaskQueue.Count > 0;

            //todo 미리 건너뛰는게 유리하지 않을까?
            if (!isHeld && !hasPendingIOs && !hasPendingTasks)
            {
                garbages ??= new List<RemoteClient>();
                garbages.Add(client);
            }




            // HoldingCounter에 의해서 잡힌 경우 아직 파괴하면 안됨.
            bool isHeld = client.HoldingCount > 0;
            if (isHeld)
                continue;

            // 아직 전송중인 경우에는 대기. (강제로 중지하는 경우에는 소켓을 닫아줘야 이 상태가 풀리게됨)
            bool isSending = client.Transport.IsSending;
            if (isSending)
                continue;

            // 아직 수신중인 경우에는 대기. (강제로 중지하는 경우에는 소켓을 닫아줘야 이 상태가 풀리게됨)
            bool isReceiving = client.Transport.IsReceiving;
            if (isReceiving)
                continue;

            // 아직 처리중인 태스크가 있는 경우에는 대기함. (바로 중지하고자할 경우에는 CancellationToken.Cancel()을 호출)
            bool hasPendingTasks = client.TaskQueue.Count > 0;
            if (hasPendingTasks)
                continue;

            // 여기서 바로 삭제가 불가능 하므로, 루프 밖에서 안전하게 처리하자.
            garbages ??= new List<RemoteClient>();
            garbages.Add(client);
        }

        //todo 반듯이 메인락을 잡은 상태에서 해야하나?
        if (garbages == null)
            return;

        // 제거해야할 클라이언트들 정리.
        // 여기서는 파괴처리해도 안전하다.
        foreach (var client in garbages)
        {
            _hardClosingRequestedClients.Remove(client);
            UnsafeDisposeClient(client);
        }
    }
}

private void UnsafeDisposeClient(RemoteClient client)
{
    // 전송큐에서 제거하자.
    // 이걸 위에서 체크하고 넘어와야하지 않을까 싶은데?

    lock (_tcpSendRequestQueueLock)
    {
        client.UnlinkFromSendRequestQueue();
    }

    // 소켓 핸들만 닫아줌. (참조는 그대로 유지함. 예외를 유발해서 상황을 정리하기 위함임. 사실상 null로 처리해도 무방. 그러나 interlocked처리가 귀찮...)
    client.Transport.CloseSocketHandleOnly();

    if (client.LinkId != LinkId.None) // 인증된 클라이언트인 경우
    {
        // 참여했던 그룹에서 나감 처리하기.
        if (client.JoinedGroups.Count > 0)
        {
            // 내부에서 목록이 수정되면 iteration exception이 떨어지므로 사본을 만든다음 수행.
            var joinedGroupIds = new List<LinkId>(client.JoinedGroupIds);
            foreach (var joinedGroupId in joinedGroupIds)
            {
                //todo callback이 있다면 async로 대응해야함.
                LeaveFromGroup(joinedGroupId, client.LinkId);
            }
        }

        _clients.Remove(client.LinkId);

        _linkIdAllocator.Free(client.LinkId, HeartbeatTime); //todo 인자로 넘겨줄까?
        client.LinkId = LinkId.None;
    }
    else // 인증되지 않은 클라이언트인 경우(후보)
    {
        _candidates.Remove(client.LinkId);
    }
}

private void PurgeTooLongUnmaturedCandidates()
{
    List<RemoteClient> list = null;

    lock (_candidatesLock) // 그냥 _clientsLock으로? 아니면 _mainLock으로?
    {
        if (_candidates.Count == 0)
            return;

        var serverTime = HeartbeatTime;
        foreach (var pair in _candidates)
        {
            var client = pair.Key;

            if ((serverTime - client.CreatedTime) <= NetworkConfig.CandidateConnectionTimeout)
                continue;

            if (client.PurgeRequested)
                continue;

            client.PurgeRequested = true;

            list ??= new List<RemoteClient>();
            list.IncreaseHoldingCount(); //락을 잡지 않고 접근하기 위해서..
            list.Add(client);
        }
    }

    if (list == null)
        return;

    // 메시지를 미리 만들어놓고 풀링을 해도 되지 않으려나?
    var kickMessage = MakeConnecToServerRejectedMessage(RtStatusCode.Timedout);
    
    foreach (var client in list)
    {
        // 락을 잡지 않은 상태이니 맨 마지막에 해주는게 맞지 않으려나?
        client.DecreaseHoldingCounter();

        // Kick 메시지 보내기
        client.SendMessage(kickMessage);

        // 모든 작업을 마무리후에 안전하게 연결이 끊길것임.
        HardCloseClient(client,
                    reason: RtStatusCodes.DisconnectFromLocal,
                    detail: "",
                    comment: null,
                    calledWhere: "PurgeTooLongUnmaturedCandidates");
    }
}

private void HardCloseClient(RemoteClient client,
                    int reason,
                    string detail = "",
                    byte[] comment = null,
                    string calledWhere = "",
                    SocketError socketError = SocketError.Success)
{
    lock (_mainLock)
    {
        // 이미 Hard-closing 중인지...?
        if (client.ClosingTicket != null)
            return;

        _logger?.Log($"Call HardCloseClient({client.LinkID}) in `{calledWhere}`.");

        client.ClosingTicket = new ClosingTicket
        {
            CreatedTime = HeartbeatTime,
            Reason = reason,
            Detail = detail,
            ShutdownComment = comment,
            SocketError = socketError
        };
        _hardClosingRequestedClients.Add(client, HeartbeatTime);

        EnqueueClientLeftEvent(client, reason, detail, comment, socketError);

        // Close the socket here to release the pending I/O state.
        client.Transport.CloseSocketHandleOnly();

        CheckTooShortClosingClient(client, "HardCloseClient");
    }
}
