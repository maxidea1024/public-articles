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
        ServerOptions = PreValidations.CheckNotNull(serverOptions).Clone();
        ServerOptions.Validates();

        // 그룹을 미리 만들어 놓아야할 경우.
        if (serverOptions.PreAssignedGroupIdStart > LinkId.Last &&
            serverOptions.PreAssignedGroupIdCount > 0)
        {
            _linkIdAllocator = new PreAssignedRemoteIdAllocator();

            // 방을 미리 생성해두는 상황이므로, 빈방을 유지하는 옵션을 강제로 활성화해야함.
            ServerOptions.AllowEmptyGroups = true;

            for (int i = 0; i < ServerOptions.PreAssignedGroupIdCount; i++)
            {
                LinkId groupId = ServerOptions.PreAssignedGroupIdStart + i;
                CreateGroup(null, null, null, groupId);
            }
        }
        else
        {
            if (ServerOptions.PreferPooledIDGeneration)
            {
                _linkIDAllocator = new RoundRobinLinkIDAllocator();
            }
            else
            {
                _linkIDAllocator = new PooledLinkIDAllocator(200); //todo 이 don't time도 설정할 수 있게 빼주자.
            }
        }

        // Set server instance uid
        _serverInstanceId = Guid.NewGuid();

        _proxyS2C = new RealtimeEngine.ProxyS2C { InternalUseOnly = true };
        _stubC2S = new RealtimeEngine.StubC2S
        {
            InternalUseOnly = true,
            ShutdownTcpAsync = StubShutdownTcpAsync,
            ShutdownTcpHandshake = StubShutdownTcpHandshakeAsync,
            ReportClientCoreLogsToServerAsync = Stub_ReportClientCoreLogsToServerAsync,
        };

        BindProxy(_proxyS2C);
        BindStub(_stubC2S);

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
        //todo 여기서 그룹을 만들고 하는게 맞으려나?

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


    // 동일한 태스크를 어디에 넣어주느냐에 따라서 실행 환경이 달라진다.

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

    public bool SetTag(LinkId remoteId, object tag)
    {
        lock (_mainLock)
        {
            // 설정 대상이 서버라면.
            if (remoteId == LinkId.Server)
            {
                _serverTag = tag;
                return true;
            }

            // 설정 대상이 클라이언트라면.
            var client = GetClient(remoteId);
            if (client != null)
            {
                client.Tag = tag;
                return true;
            }

            // 설정 대상이 그룹이라면.
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

    // 서버가 시작 되었음을 확인.
    private void CheckServerStarted(string calledWhere)
    {
    }

    // 서버가 시작 안되었음을 확인.
    private void CheckServerNotStarted(string calledWhere)
    {
    }

    // RpcProxy로 메시지를 보낼때 사용하는 전용 함수.
    public override bool SendByProxy(Message message, RtSendOptions options, LinkId[] sendTo)
    {
        // 메시지를 어떤식으로 처리해야 자연스러울까?
    }

    // 현재 접속되어 있는 클라이언트들의 목록을 조회함.
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

    // 강제로 끊기
    internal void HardCloseClient(RemoteClient client,
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

            //todo 얘도 풀링해야하나?
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
            CheckTooShortClosingClient(client, "HardCloseClient");
        }
    }

    // 접속하자마자 연결이 끊기는 경우인지 체크.
    // 버그로 인해서 끊기는 상황일 수 있으므로, 확인하기 편할 수 있다.
    private void CheckTooShortClosingClient(RemoteClient client, string calledWhere)
    {
        // 중복 호출 막기
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

    // 클라이언트가 끊긴 이벤트를 큐잉한다.
    private void EnqueueClientLeftEvent(RemoteClient client,
                                int reason,
                                string detail,
                                byte[] comment,
                                ocketError socketError)
    {
        // Candidate는 이벤트를 통지 받지 않는다.
        if (client.LinkId == LinkId.None)
        {
            return;
        }

        //각 태스크에 메시지로 넣어주면 되는건가?

        //todo DoubleBufferedQueue<T>를 사용하면 될듯한데..

        //todo 각각의 task큐에 이벤트를 넣어주고 async-await로 처리하면 되는..

        client.EnqueueEvent(new ServerLocalEvent
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

    /// <summary>
    /// Soft closing모드에서 너무 오래 머무르고 있으면 강제로 Hard closing모드로 전환한다.
    /// </summary>
    private void SwitchToHardClosingModeIfSoftClosingGoesTooLongClient(RemoteClient client)
    {
        if (client.SoftClosingRequestedTime != 0 &&
            (HeartbeatTime - client.SoftClosingRequestedTime) > NetworkConfig.ClientSoftClosingTimeout)
        {
            if (_logger != null)
            {
                _logger.Log($"Client {client.RemoteId} is asked to disconnect by itself, but the connection is not disconnected for {NetworkConfig.ClientSoftClosingTimeout}ms, so it switches to forced disconnect mode.");
            }

            // Soft-closing 모드는 해제한다.
            client.SoftClosingRequestedTime = 0;

            // Hard-closing 모드로 전환한다.
            HardCloseClient(client,
                    reason: RtStatusCodes.DisconnectFromLocal,
                    detail: "",
                    comment: null,
                    calledWhere: "SwitchToHardClosingModeIfSoftClosingGoesTooLongClient");
        }
    }

    /// <summary>
    /// Hard closing 모드에 있는 리모트들을 정리한다.
    /// </summary>
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

                // 너무 오랫동안 머물고 있는 경우에는 경고를 내주어서 해당 내용을 알 수 있도록 해주자.

                // 필요에 의해서 홀드 카운터를 올린 경우.
                bool isHeld = client.HoldingCount > 0;

                // 보내기 중인지.
                bool isSending = client.Transport.IsSending;

                // 받기 중인지(소켓을 닫아주기전까지는 계속 받기 상태로 나옴)
                bool isReceiving = client.Transport.IsReceiving;

                // IO 처리중인지.
                nool hasPendingIOs = isSending || isReceiving;

                // 아직 처리중인 작업이 있는지.
                bool hasPendingTasks = client.PendingTaskCount > 0;

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

    /// <summary>
    /// 지정한 클라이언트를 제거함. 직접 호출하면 안됨.
    /// </summary>
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
            //todo 이게 맞는걸까?
            //여기까지 올수가 있긴하네...
            _candidates.Remote(client.LinkId);
        }
    }

    /// <summary>
    /// 너무 오랜기간동안 승인받지 못한 Candidate들을 제거한다.
    /// </summary>
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

                client.DecreaseHoldingCount();

                client.SendMessage(kickMessage);

                HardCloseClient(client,
                        reason: RtStatusCodes.DisconnectFromLocal,
                        detail: "",
                        comment: null,
                        calledWhere: "PurgeTooLongUnmaturedCandidates");
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
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

            // Close the socket here to release the pending I/O state.
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
                return _clients.Count;
        }
    }

    public int CandidateCount
    {
        get
        {
            lock (_mainLock)
                return _candidates.Count;
        }
    }

    public int SupendedCount
    {
        get
        {
            lock (_mainLock)
                return _suspendeds.Count;
        }
    }

    // Close all connected clients.
    // It does not close immediately, but after going through the Soft-closing -> Hard-closing process, the connection is released.
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

    /// <summary>
    /// Close the specified client.
    /// </summary>
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

    /// <summary>
    /// main lock을 걸고 들어오므로 안전함.
    /// </summary>
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

    /// <summary>
    /// Create a group.
    /// </summary>
    public LinkId CreateGroup(LinkId[] members = null, RtGroupOptions = null, byte[] userData = null, LinkId preAssignedLinkId = LinkId.None)
    {
        options ??= new RtGroupOptions();

        lock (_mainLock)
        {
            LinkId groupId =
        }
    }

    /// <summary>
    /// Join the specified member to the group.
    /// </summary>
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

    /// <summary>
    /// 지정한 멤버를 그룹에서 내보낸다.
    /// </summary>
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

    // 클라이언트가 먼저 연결을 끊는 경우, ShutdownTcp를 서버에 요청한다.
    private async Task StubShutdownTcpAsync(LinkId clientId, RtRpcContext context, byte[] comment)
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
        _proxyS2C.ShutdownTcpAck(clientId, RtRpcCallOptions.ReliableCoreOnly);
    }

    // 클라에서 ShutdownTcp를 받은 직후 서버는 연결을 끊게되며,
    // 클라가 끊을때 보낸 comment(사용자 메시지)를 핸들러로 넘겨받게 된다.
    private async Task StubShutdownTcpHandshakeAsync(LinkId clientId, RtRpcContext context)
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

    // 클라디버깅을 위해서, 옵션이 활설화 되었을 경우 서버에서 클라의 내부 로그를 수집한다.
    private async Task StubReportClientCoreLogsToServerAsync(LinkId clientId, RtRpcCotnext context, string message)
    {
        if (_logger != null)
        {
            _logger.Log($"[CLIENT {remote}] {message}");
        }
    }

    #endregion


    /// <summary>
    /// 그룹 목록이 있을 경우에는 확장 시켜줘야함.
    /// 루프백도 지원할지 여부를 결정해야함.
    /// 최적화를 어떻게 할지에 대해서 고민해보는게 좋을듯한데..
    /// </summary>
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

    public override void EnqueueWarningEvent(RtStatus info)
    {
        //todo
        //서버 태스크 큐에 집어넣어주자.
    }

    public override void EnqueueErrorEvent(RtStatus info)
    {
        //todo
        //서버 태스크 큐에 집어넣어주자.
    }

    public override void EnqueueInformationEvent(RtStatus info)
    {
        //todo
        //서버 태스크 큐에 집어넣어주자.
    }


    /// <summary>
    /// 일감 하나를 요청함.
    /// </summary>
    private void EnqueueTask(LinkId taskOwnerId, LocalEvent localEvent)
    {
        var workItem = new UserWorkItem(localEvent);
        EnqueueTask(taskOwnerId, workItem);
    }

    /// <summary>
    /// 일감 하나를 요청함.
    /// </summary>
    private void EnqueueTask(LinkId taskOwnerId, UserWorkItemType type, IncomingMessage receivedMessage)
    {
        var workItem = new UserWorkItem(receivedMessage, type);
        EnqueueTask(taskOwnerId, workItem);
    }

    /// <summary>
    /// 일감 하나를 요청함.
    /// </summary>
    private void EnqueueTask(LinkId taskOwnerId, UserWorkItem workItem)
    {
        if (taskOwnerId == LinkId.None)
        {
            return;
        }

        if (workItem == null)
        {
            return;
        }

        if (taskOwnerId == LinkId.Server)
        {
            ServerTaskQueue.Enqueue(workItem);
            return;
        }

        lock (_mainLock)
        {
            var client = GetClient(taskOwnerId);
            if (client != null)
            {
                client.TaskQueue.Enqueue(workItem);
                return;
            }

            var group = GetGroup(taskOwnerId);
            if (group != null)
            {
                group.TaskQueue.Enqueue(workItem);
                return;
            }
        }
    }

    /// <summary>
    /// 사용자 일감 하나를 처리함.
    /// </summary>
    private async Task DoUserWorkAsync(UserWorkItem workItem)
    {
        try
        {
            if (workItem.Type == UserWorkItem.RPC)
            {
                await DoRpcAsync(workItem);
            }
            else if (workItem.Type == UserWorkItem.LocalEvent)
            {
                await DoLocalEventAsync(workItem);
            }
            else
            {
                // do something...
            }
        }
        catch (Exception e)
        {
        }
    }

    /// <summary>
    /// 로컬 이벤트 하나를 처리함.
    /// </summary>
    private async Task DoLocalEventAsync(UserWorkItem workItem)
    {
        var localEvent = workItem.LocalEvent;

        try
        {
            if (localEvent.Type == LocalEventType.ClientJoinDetermine)
            {
                byte[] reply = null;
                bool approved = true;

                if (HasConnectionRequestCallback)
                {
                    approved = await InvokeConnectionRequestCallback(localEvent.RemoveEndPoint, localEvent.UserData, out reply);
                }

                using (_mainLock)
                {
                    var client = GetCandidateByTcpAddress(localEvent.RemoteEndPoint);
                    if (client != null)
                    {
                        if (approved)
                        {
                            HandleConnectionJoinApproved(client, reply);
                        }
                        else
                        {
                            HandleConnectionJoinRejected(client, reply);
                        }
                    }
                }
            }
            else if (localEvent.Type == LocalEventType.ClientJoined)
            {
                await InvokeClientJoinedCallbackAsync(localEvent.ClientInfo);
            }
            else if (localEvent.Type == LocalEventType.ClientLeft)
            {
                await InvokeClientLeftCallbackAsync(localEvent.ClientInfo, localEvent.Status, localEvent.UserData);
            }
            else if (localEvent.Type == LocalEventType.Information)
            {
                await InvokeInformationCallbackAsync(localEvent.Status);
            }
            else if (localEvent.Type == LocalEventType.Warning)
            {
                await InvokeWarningCallbackAsync(localEvent.Status);
            }
            else if (localEvent.Type == LocalEventType.Error)
            {
                await InvokeErrorCallbackAsync(localEvent.Status);
            }
            else
            {
                // What?
            }
        }
        catch (Exception e)
        {
            //todo 이중으로 오류가 발생한다면?
            try
            {
                await InvokeExceptionCallbackAsync(localEvent.RemoteId, e);
            }
            catch (Exception e2)
            {
                //todo Double exception. 를 로그에 기록해주자.
            }

            //어쨌거나 연결을 끊어야하는거 아닌가?
        }
    }

    /// <summary>
    /// 연결이 승인되었음을 알림.
    /// </summary>
    private void HandleConnectionJoinApproved(RemoteClient client, byte[] reply)
    {
        // 이미 메인락을 잡고 들어옴.

        LinkId linkId = _linkIdAllocator.Allocate(HeartbeatTime);
        client.LinkId = linkId;

        _candidates.Remove(client);
        _clients.Add(linkId, client);

        // Send `ConnectToServerSuccess` message to client.
        var response = new MessageOut();
        response.Write(CoreMessageType.ConnectToServerSuccess);
        response.Write(linkId);
        response.Write(_serverInstanceId);
        response.Write(reply);
        response.Write(client.ExternalAddress);
        client.SendMessage(response);

        EnqueueLocalEvent(new ServerLocalEvent
        {
            Type = LocalEventType.ClientJoinApproved,
            ClientInfo = client.GetInfo()
        });
    }

    /// <summary>
    /// 연결이 거부 되었음을 알림.
    /// </summary>
    private void HandleConnectionJoinRejected(RemoteClient client, byte[] reply)
    {
        // 이미 메인락을 잡고 들어옴.

        RejectCandidateConnection(client,
                RtSttusCodes.ConnectToServerDenied,
                reply: reply,
                calledWhere: "HandleConnectionJoinRejectedAsync");
    }


    #region Listening

    private void StartListening()
    {
        if (ServerOptions.UseDynamicListeningPort)
        {
            //소켓을 여러게 만들고 처리하자.
            var listener = new Socket();
            _listeners.Add(listener);
        }
        else
        {
            foreach (int listeningPort in ServerOptions.ListeningPorts)
            {
                var listener = new RtTcpListener();
                listener.NewConnection += OnNewConnection;
                listener.StartListening(ServerOptions.ServerIp, listeningPort);

                _listeners.Add(listener);
            }
        }
    }

    private void StopListening()
    {
        lock (_listeningLock)
        {
            if (_listeners != null)
            {
                foreach (var listener in _listeners)
                {
                    listener.CloseSocketHandleOnly();
                }

                _listeners.Clear();
            }
        }
    }

    //Accept async loop

    #endregion
