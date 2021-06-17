//todo 싱글스레드로 동작하도록 한다면?
//todo 메모리 풀링을 극단적으로 할 수 있다면?

namespace Lane.Realtime.Server.Internal
{
    /// <summary>
    /// 접속해제시 접속 해제 사유가 기록됨.
    /// </summary>
    internal class ClosingTicket
    {
        public int Reason { get; set; }

        public string Detail { get; set; }

        public byte[] ShutdownComment { get; set; }

        public long CreatedTime { get; set; }

        public SocketError SocketError { get; set; }
    }
}

    //todo Main lock 사용을 최소화하자.
    //재접속일 경우에는 기존 remote를 찾아서 태그를 교체해주자.
    //기존 remote는 제거해주는데 대신에
    //수신중이었던 메시지는 어떻게 처리해야하나?
    //전송 계층을 분리해야하나?

    public RtServer(RtServerOptions serverOptions)
    {
        //todo 이건 Start에서 해주는게 맞지 않나?

        // 그룹을 미리 만들어 놓아야할 경우.
        if (serverOptions.PreAssignedGroupIdStart > LinkId.Last &&
            serverOptions.PreAssignedGroupIdCount > 0)
        {
            _linkIdAllocator = new PreAssignedRemoteIdAllocator();

            // 방을 미리 생성해두는 상황이므로, 빈방을 유지하는 옵션을 강제로 활성화해야함.
            ServerOptions.AllowEmptyGroups = true;

            for (int i = 0; i < ServerOptions.PreAssignedGroupIdCount; i++)
            {
                //todo
                //그룹을 미리 만들어둔다.
                //해당 그룹은 방과 같은 개념이다.
            }
        }

        _serverInstanceId = Guid.NewGuid();

        //todo 내부 메시지를 보내는데 구지 RPC를 사용할 필요는 없어보임.

        // 미리 만들어둔다.
        _remoteConnectionConfig = new RemoteConnectionConfig
        {
            PingInterval = ServerOptions.PingInterval,
            PingTimeout = ServerOptions.PingTimeout,
            ReportClientCoreLogsToServer = ServerOptions.ReportClientCoreLogsToServer
        };
    }

    // 시작을 하는데 딱히 준비할게 없네?
    public async Task StartAsync()
    {
        using (_mainLock)
        {
            if (_listeners.Count > 0)
                throw new InvalidOperationException("Server is aready started.");

            StartListening();

            StartHeartbeatTimer();
        }
    }

    public async Task StopAsync()
    {
        using (_mainLock)
        {
            // 이미 종료된 상황이라면 재진입처리 안하게.
            if (_listeners.Count == 0)
            {
                return;
            }

            // Stop heartbeat timer.
            StopHeartbeatTimer();

            // 종료중임을 표시한다.
            _shutdowning = true;

            // Close all listeners.
            CloseListeners();

            // Close all clients.
            CloseAllClients();

            bool gracefullyTerminated = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.Seconds < 10) // timeout 수치는 상수로 하던지 RtServerOptions에서 지정하도록 하자.
            {
                if (_clients.Count == 0 && _candidates.Count == 0 && _taskQueue.Count == 0)
                {
                    gracefullyTerminated = true;
                    break;
                }

                await Task.Delay(100);
            }

            if (!gracefullyTerminated)
            {
                //설령 이렇게 취소를 한다고 해도 바로 끝나리란 보장이 없지않나?
                //_taskQueue.Cancel();
                _cts.Cancel();
            }

            UnsafeCleanup();
        }
    }

    public void Wait()
    {
        var waitEvent = new ManualResetEvent(false);

        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            waitEvent.Set();
        };

        waitEvent.WaitOne();
    }

    public void Run()
    {
        Start();

        Wait();
    }

    private void UnsafeCleanup()
    {
        lock (_mainLock)
        {
            _candidates.Clear();
            _clients.Clear();
            _hardClosingRequestedClients.Clear();
            _groups.Clear();
        }
    }

    public override void EnqueueWarning(RtStatus info)
    {
        //todo
    }


    Task.Run(async () => await ProcessServerTasksAsync());

    // Task큐에 집어 넣어주고 빠진다.
    private async Task ProcessServerTasksAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (_tasks.TryDequeue(out var task))
            {
                // 한번에 하나씩만 처리함.
                await task();
            }
        }
    }

    //각 리모트에도 하나의 태스크 루프가 있음.
    private async Task ProcessPerClientTasksAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (_tasks.TryDequeue(out var task))
            {
                // 한번에 하나씩만 처리함.
                await task();
            }
        }
    }

    //그룹도 같이 처리함.
    private async Task ProcessPerGroupTasksAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (_tasks.TryDequeue(out var task))
            {
                // 한번에 하나씩만 처리함.
                await task();
            }
        }
    }

    // 자체 메시지 태스크로 넣어줘야함.
    // ServerLocalEvent객체는 풀링될수 없는건가?
    private void EnqueueServerLocalEvent(ServerLocalEvent localEvent)
    {
        lock (_mainLock)
        {
            //if (_shutdowning)
            //    return;
            if (_listeners.Count == 0)
            {
                return;
            }
        }

        lock (_taskQueueLock)
        {
            _taskQueue.Enqueue(localEvent);
        }

        //todo Task.Run()으로 루핑 태스크를 돌려주고 이벤트를 뽑아내서 처리하는 형태로 구현.

        // Task.Run()으로 처리하고 있으려나?
        //EnqueueTask(LinkId.Server, localEvent);
    }

    public bool SetRemoteTag(LinkId remoteId, object tag)
    {
        lock (_mainLock)
        {
            if (remoteId == LinkId.Server)
            {
                _serverTag = tag;
                return true;
            }

            var client = GetClient(remoteId);
            if (client != null)
            {
                client.Tag = tag;
                return true;
            }

            var group = GetGroup(remoteId);
            if (group != null)
            {
                group.Tag = tag;
                return true;
            }

            return false;
        }
    }

    public RtServerInfo GetServerInfo()
    {
        lock (_mainLock)
        {
            //todo
        }
    }

    private void CheckServerStarted(string calledWhere)
    {
        // 서버가 시작 되었음을 확인하는..
    }

    // RpcProxy로 메시지를 보낼때 사용하는 전용 함수.
    public override bool SendByProxy(Message message, RtSendOptions options, LinkId[] sendTo)
    {
        // 메시지를 어떤식으로 처리해야 자연스러울까?
    }

    public int GetClientIds(ref List<LinkId> clientIds)
    {
        clientIds.Clear();

        lock (_mainLock)
        {
            foreach (var pair in _clients)
            {
                clientIds.Add(pair.Key);
            }

            return clientIds.Count;
        }
    }

    internal void HardCloseConnection(RemoteClient client,
                                    int reason,
                                    string detail = "",
                                    byte[] comment = null,
                                    string calledWhere = "",
                                    SocketError socketError = SocketError.Success)
    {
        lock (_mainLock)
        {
            //todo Interlocked로 체크해야하지 않을까 싶은데..
            //근데, 어짜피 메인락을 걸고 들어오므로 이렇게해도 무방할듯 싶다.
            if (client.ClosingTicket != null)
            {
                return;
            }

            if (_logger != null)
            {
                _logger.Log($"Call HardCloseClient({client.LinkID}) in `{calledWhere}`.");
            }

            client.ClosingTicket = new ClosingTicket
            {
                CreatedTime = HeartbeatTime,
                Reason = reason,
                Detail = detail,
                ShutdownComment = comment,
                SocketError = socketError
            };

            _hardClosingRequestedClients.Add(client, HeartbeatTime);

            QueueClientLeftEvent(client, reason, detail, comment, socketError);

            // 전송 계층을 닫아줌.
            client.Transport.CloseSocketHandleOnly();

            // 너무 자주 끊기는지 여부 확인. (버그 확인을 위함)
            CheckTooShortClosingClient(client, "HardCloseConnection");
        }
    }

    private void CheckTooShortClosingClient(RemoteClient client, string calledWhere)
    {
        if (client.DisposeCaller != null)
        {
            return;
        }

        client.DisposeCaller = calledWhere;

        if (_logger != null &&
            (HeartbeatTime - client.CreatedTime) < NetworkConfig.TooShortClosingClientThreshold)
        {
            _logger.Log($"As soon as client(linkID:{client.LinkID}, address:`{client.ExternalAddress}`) connected, the connection was disconnected. This could be the intended behavior or it could be a bug. in `{calledWhere}`");
        }
    }

    private void EnqueueClientLeftEvent(RemoteClient client,
                                int reason,
                                string detail,
                                byte[] comment,
                                ocketError socketError)
    {
        if (client.LinkId == LinkId.None)
        {
            return;
        }

        //각 태스크에 메시지로 넣어주면 되는건가?

        //todo DoubleBufferedQueue<T>를 사용하면 될듯한데..

        //todo 각각의 task큐에 이벤트를 넣어주고 async-await로 처리하면 되는..

        lock (client._taskQueueLock)
        {
            client._taskQueue.Enqueue(new ServerLocalEvent
            {
                Type = ServerLocalEventType.ClientDisposed,
                Status = new RtStatus
                {
                    StatusCode = reason,
                    Detail = detail,
                    LinkId = client.LinkId
                },
                RemoteInfo = client.GetInfo(),
                UserData = comment,
                LinkId = client.LinkId,
                SocketError = socketError
            });
        }

        //EnqueueTask(client.LinkId,
        //    new ServerLocalEvent
        //    {
        //        Type = ServerLocalEventType.ClientDisposed,
        //        Status = new RtStatus
        //        {
        //            StatusCode = reason,
        //            Detail = detail,
        //            LinkId = client.LinkId
        //        },
        //        RemoteInfo = client.GetInfo(),
        //        UserData = comment,
        //        LinkId = client.LinkId,
        //        SocketError = socketError
        //    });
    }

    // Soft closing모드에서 너무 오래 머무르고 있으면 강제로 Hard closing모드로 전환한다.
    private void SwitchToHardClosingModeIfSoftClosingGoesTooLongClient(RemoteClient client)
    {
        if (client.SoftClosingRequestedTime != 0 &&
            (HeartbeatTime - client.SoftClosingRequestedTime) > NetworkConfig.ClientSoftClosingTimeout)
        {
            client.SoftClosingRequestedTime = 0;

            if (_logger != null)
            {
                _logger.Log($"Client {client.RemoteId} is asked to disconnect by itself, but the connection is not disconnected for {NetworkConfig.ClientSoftClosingTimeout}ms, so it switches to forced disconnect mode.");
            }

            HardCloseClient(client,
                    reason: RtStatusCodes.DisconnectFromLocal,
                    detail: "",
                    comment: null,
                    calledWhere: "SwitchToHardClosingModeIfSoftClosingGoesTooLongClient");
        }
    }

    // Hard closing 모드에 있는 리모트들을 정리한다.
    private void PurgeHardClosingRequestedClients()
    {
        lock (_mainLock)
        {
            if (_hardClosingRequestedClients.Count == 0)
            {
                return;
            }

            List<RemoteClient> garbages = null;
            foreach (var pair in _hardClosingRequestedClients)
            {
                var client = pair.Key;
                var requestedTime = pair.Value;

                // 필요에 의해서 홀드 카운터를 올린 경우.
                bool isHeld = client.HoldingCount > 0;

                // 보내기 중인지.
                bool isSending = client.Transport.IsSending;

                // 받기 중인지(소켓을 닫아주기전까지는 계속 받기 상태로 나옴)
                bool isReceiving = client.Transport.IsReceiving;

                // IO 처리중인지.
                nool hasPendingIOs = isSending || isReceiving;

                // 아직 처리중인 작업이 있는지.
                bool hasPendingTasks = client.PendingTaskCount > 0; //너무 오랫동안 대기할 경우에는 워닝을..

                if (!isHeld && !hasPendingIOs && !hasPendingTasks)
                {
                    garbages ??= new List<RemoteClient>();
                    garbages.Add(client);
                }
            }

            if (garbages != null)
            {
                foreach (var client in garbages)
                {
                    _hardClosingRequestedClients.Remote(client); // 키로 제거하는게 좋을듯..
                    UnsafeDisposeClient(client);
                }
            }
        }
    }

    // 지정한 클라이언트를 제거함.
    // 직접 호출하면 안됨.
    private void UnsafeDisposeClient(RemoteClient client)
    {
        // 전송 큐에서 제거함.
        lock (_tcpSendRequestQueueLock)
        {
            client.UnlinkFromSendRequestQueue();
        }

        // 소켓을 닫아줌.
        client.Transport.CloseSocketHandleOnly();

        // 그룹에서 나갔음을 처리.
        if (client.LinkId != LinkId.None)
        {
            if (client.JoinedGroups.Count > 0)
            {
                var joinedGrous = new List<LinkId>(client.JoinedGroups);

                foreach (Var joinedGroupId in joinedGroups)
                {
                    LeaveFromGroup(joinedGroupId, client.LinkId);
                }
            }

            _clients.Remove(client.LinkId);

            _linkIdAllocator.Free(client.LinkId, HeartbeatTime);
            client.LinkId = LinkId.None;
        }
        else
        {
            _candidates.Remote(client.LinkId);
        }
    }

    private void PurgeTooLongUnmaturedCandidates()
    {
        List<RemoteClient> list = null;

        lock (_mainLock)
        {
            // 후보가 없을 경우에는 아무런 처리도 하지 않음.
            if (_candidates.Count == 0)
            {
                return;
            }

            // 오랜시간동안 인증되지 못한 후보 클라이언트들 정리.
            var serverTime = HeartbeatTime;
            foreach (var pair in _candidates)
            {
                var client = pair.Key;

                if ((serverTime - client.CreatedTime) <= NetworkConfig.CandidateConnectionTimeout)
                {
                    continue;
                }

                if (!client.PurgeRequested)
                {
                    list.PurgeRequested = true;

                    list ??= new List<RemoteClient>();
                    client.IncreaseHoldingCount();
                    list.Add(client);
                }
            }
        }

        // Soft closing하지 않고, 종료 메시지를 보낸다음 Hard closing을 개시한다.
        if (list != null)
        {
            // 타임아웃 메시지.
            var kickMessage = MakeConnectToServerRejectedMessage(RtStatusCode.Timedout);

            foreach (var client in list)
            {
                // 동일한 메시지를 여러군데 보낼 경우에는 한곳에서 보냈다고 해서 제거하면 안되는데?
                // 이게 크리티컬 하구나.
                // 여러군대 보낼때는 메시지를 클론해서 보내야한다는 얘긴데?
                // 동일한 메시지를 여러군데 보낼때는 어떻게 처리할지를 생각해보는 맞을듯한데...??
                client.SendMessage(kickMessage);
                client.DecreaseHoldingCount();

                HardCloseClient(client,
                        reason: RtStatusCodes.DisconnectFromLocal,
                        detail: "",
                        comment: null,
                        calledWhere: "PurgeTooLongUnmaturedCandidates");
            }
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
            if (client.ClosingTicket != null)
            {
                return;
            }

            if (_logger != null)
            {
                CoreLogger.Log($"Call HardCloseClient({client.LinkID}) in `{calledWhere}`.");
            }

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

            // 여기서 소켓을 닫아줘야 pending io 상태가 해제됨.
            client.Transport.CloseSocketHandleOnly();

            CheckTooShortClosingClient(client, "HardCloseClient");
        }
    }


    internal OutgoingMessage MakeConnectToServerRejectedMessage(int reason, string detail = null, byte[] reply = null)
    {
        //하나의 메시지를 여러곳에 보낼 경우에는 참조카운팅이 필요할수 있다.
        //브로드캐스팅 함수를 하나 만들어서 처리하는게 좋을듯하다.
        //메시지 빌드를 어떤식으로 해야하나?
    }

    internal void RejectCandidateConnection(RemoteClient client, Message rejectMessage, string calledWhere = "")
    {
        client.SendMessage(rejectMessage);

        HardCloseClient(client,
                reason: RtStatusCodes.DisconnectFromLocal,
                detail: "",
                comment: null,
                calledWhere: calledWhere);
    }


    public bool IsValidClient(LinkId remoteId)
    {
        return GetClient(remoteId) != null;
    }

    public int ClientCount
    {
        get
        {
            lock (_mainLock)
            {
                return _clients.Count;
            }
        }
    }

    public int CandidateCount
    {
        get
        {
            lock (_mainLock)
            {
                return _candidates.Count;
            }
        }
    }

    public int SupendedCount
    {
        get
        {
            lock (_mainLock)
            {
                return _suspendeds.Count;
            }
        }
    }

    // 연결된 모든 클라이언트를 닫아줌.
    // 바로 닫히지는 않고 Soft closing -> Hard closing 과정을 거친 후 접속이 해제됨.
    public void CloseAllClients()
    {
        lock (_mainLock)
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

    // 지정한 클라이언트를 닫아줌.
    public void CloseClient(LinkId clientId)
    {
        using (_mainLock)
        {
            var client = GetClient(clientId);

            if (client != null)
            {
                if (_logger != null)
                {
                    _logger.Log($"Call CloseConnection(clientId:{clientId})");
                }

                RequestSoftClosingToClient(client);
            }
        }
    }

    // main lock을 걸고 들어오므로 안전함.
    private void RequestSoftClosingToClient(RemoteClient client)
    {
        if (client.SoftClosingRequestedTime != 0)
        {
            client.SoftClosingRequestedTime = HeartbeatTime;

            if (_logger != null)
            {
                _logger.Log($"Call RequestSoftClosingToClient({client.LinkId})");
            }

            _proxyS2C.RequestSoftClosingToClient(client.LinkId, RtRpcCallOptions.ReliableCoreOnly);
        }
    }

    public LinkId CreateGroup(LinkId[] members = null, RtGroupOptions = null, byte[] userData = null, LinkId preAssignedLinkId = LinkId.None)
    {
        options ??= new RtGroupOptions();

        lock (_mainLock)
        {
            LinkId groupId =
        }
    }

    public bool JoinIntoGroup(LinkId groupId, LinkId memberId, byte[] userData = null)
    {
        if (userData == null)
            userData = Array.Empty<byte>();

        lock (_mainLock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            var member = GetClient(memberId);
            if (member == null)
            {
                return false;
            }

            if (!group.Join(member))
            {
                return false;
            }

            foreach (var m in group.Members)
            {
                _proxyS2C.GroupMemberJoined(m, (int)memberId, (int)groupId, group.MemberCount, userData);
            }

            foreach (var m in group.Members)
            {
                if (m != memberId)
                {
                    _proxyS2C.GroupMemberJoined(memberId, (int)m, (int)groupId, group.MemberCount, userData);
                }
            }

            return true;
        }
    }

    public bool LeaveGroup(LinkId groupId, LinkId memberId)
    {
        lock (_mainLock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
            {
                return false;
            }

            var member = GetClient(memberId);
            if (member == null)
            {
                return false;
            }

            if (!group.IsMember(memberId))
            {
                return false;
            }

            group.Leave(memberId);

            foreach (var m in group.Members)
            {
                _proxyS2C.GroupMemberLeft(m, (int)memberID, (int)groupID, group.Members.Count);
            }

            if (!ServerOptions.AllowEmptyGroups && group.MemberCount == 0)
            {
                _groups.Remove(groupId);
            }

            return true;
        }
    }

    public void DestroyEmptyGroups()
    {

    }


    #region C2S RPC Stub

    private async Task Stub_ShutdownTcpAsync(LinkId clientId, RtRpcContext context, byte[] comment)
    {
        lock (_mainLock)
        {
            var client = GetClient(clientId);
            if (client != null)
            {
                client.ShutdownComment = comment;
            }
        }

        // RPC를 사용하지 않고 직접 메시지를 보내도 되지 않을까?
        _proxyS2C.ShutdownTcpAck(remote, RtRpcCallOptions.ReliableCoreOnly);
    }

    private async Task Stub_ShutdownTcpHandshakeAsync(LinkId clientId, RtRpcContext context)
    {
        lock (_mainLock)
        {
            var client = GetClient(clientId);
            if (client == null)
            {
                return;
            }

            HardCloseClient(client,
                    reason: RtStatusCodes.DisconnectFromRemote,
                    detail: "ShutdownTcpRequestedByClient",
                    comment: client.ShutdownComment,
                    calledWhere: "Stub_ShutdownTcpHandshakeAsync");
        }
    }

    private async Task Stub_ReportClientCoreLogsToServerAsync(LinkId clientId, RtRpcCotnext context, string message)
    {
        if (_logger != null)
            _logger.Log($"[CLIENT {remote}] {message}");
    }

    #endregion

    // 그룹 목록이 있을 경우에는 확장 시켜줘야함.
    // 루프백도 지원할지 여부를 결정해야함.
    // 최적화를 어떻게 할지에 대해서 고민해보는게 좋을듯한데..
    public override void ExpandSendToList(LinkId[] input, ref LinkId[] output)
    {
        lock (_mainLock)
        {
            var list = new List<LinkId>();
            ExpandSendToListCore(input, ref list);

            list = Utility.ToFlattenList<LinkId>(list, LinkIdComparer.Instance);

            Array.Resize<LinkId>(ref output, list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                output[i] = list[i];
            }
        }
    }

    private void ExpandSendToListCore(LinkId[] input, ref List<LinkId> output)
    {
        foreach (var i in input)
        {
            ExpandSendToListCore(i, ref output);
        }
    }

    private void ExpandSendToListCore(LinkId input, ref List<LinkId> output)
    {
        lock (_mainLock)
        {
            var group = GetGroup(input);
            if (group != null)
            {
                foreach (var memberId in group.Members)
                {
                    output.Add(memberId);
                }
            }
            else
            {
                output.Add(input);
            }
        }
    }


    public override void EnqueueWarning(RtStatus info)
    {
        //todo
        //서버 태스크 큐에 집어넣어주자.
    }
