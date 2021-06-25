
    private void StartHeartbeatTimer()
    {
        _timeSource.Start();

    }

    private void StopHeartbeatTimer()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Stop();
            _heartbeatTimer = null;
        }

        _tickables.Clear();
        _timeSource.Stop();
    }

    private void HeartbeatTimerCallback(int deltaTime)
    {
        // 해당 큐에 던지면 async-await로 처리해도 무방하다는 결론?
        EnqueueTask(LinkId.Server, HeartbeatAsync);
    }

    private async Task HeartbeatAsync()
    {
        int deltaTime;
        long heartbeatTime;

        lock (_timeSourceLock)
        {
            long prevTime = _heartbeatTime;
            _heartbeatTime = _timeSource.Milliseconds;
            _heartbeatDeltaTime = (int)(_heartbeatTime - prevTime);

            deltaTime = _heartbeatDeltaTime;
            heartbeatTime = _heartbeatTime;
        }

        try
        {
            _tickables.Advance(deltaTime);
        }
        catch (Exception e)
        {
            InvokeExceptionCallback(LinkId.Server, e);
        }

        // Invoke `Tick` callback.
        if (HasTickCallback)
        {
            try
            {
                InvokeTickCallback();
            }
            catch (Exception e)
            {
                InvokeExceptionCallback(LinkId.Server, e);
            }
        }

        _timer.Update(heartbeatTime);
    }

    private void SetupTickables()
    {
        _tickables.Add(new LoopingTickable("PurgeTooLongCandidates", 15_300, x => PurgeTooLongCandidates());
        _tickables.Add(new LoopingTickable("PurgeHardClosingRequestedClients", 200, x => PurgeHardClosingRequestedClients());
        _tickables.Add(new LoopingTickable("HeartbeatPerClient", 5_000, x => HeartbeatPerClient());

        //todo 이 기능이 정상동작하려면, 초정밀 타이머가 필요하다.
        _tickables.Add(new LoopingTickable("FlushPerClientSendQueue", 5, x => FlushPerClientSendQueue());
    }

    private void HeartbeatPerClient()
    {
        lock (_mainLock)
        {
            long serverTime = HeartbeatTime;

            foreach (var pair in _clients)
            {
                var client = pair.Value;
                var nonNetworkingTime = serverTime - client.LastTcpStreamReceivedTime;
                if (nonNetworkingTime > NetworkConfig.KeepAliveTimeout)
                {
                    // 이미 종료중이 아니면 종료처리.
                    if (client.ClosingTicket == null)
                    {
                        _logger?.Log($"The TCP receive from the client {client.LinkId} no longer. Close the socket.");

                        HardCloseClient(client,
                                reason: RtStatusCodes.DisconnectFromRemote,
                                detail: "Ping timedout",
                                comment: null,
                                calledWhere: "HeartbeatPerClient",
                                socketError: SocketError.Success);
                    }
                }
                else
                {
                    SwitchToHardClosingModeIfSoftClosingGoesTooLongClient(client);

                    ConditionalFallbackServerUdpToTcp(client, serverTime);

                    ConditionalArbitraryUdpTouch(client, serverTime);

                    RefreshSendQueueAmountStats(client);
                }
            }
        }
    }


    #region Group

    private readonly Dictionary<LinkId, RtGroup> _groups = new Dictionary<LinkId, RtGroup>();

    /// <summary>
    /// </summary>
    public int GroupCount
    {
        get
        {
            lock (_mainLock)
                return _groups.Count;
        }
    }

    /// <summary>
    /// </summary>
    public bool IsValidGroup(LinkId groupId)
    {
        lock (_mainLock)
        {
            return _groups.ContainsKey(groupId);
        }
    }

    /// <summary>
    /// </summary>
    public List<LinkId> GetGroupIds()
    {
        lock (_mainLock)
        {
            var result = new List<LinkId>(_groups.Count);
            GetGroupIds(ref result);
            return result;
        }
    }

    /// <summary>
    /// </summary>
    public int GetGroupIds(ref List<LinkId> groupIds)
    {
        groups.Clear(); // Just in case
        
        lock (_mainLock)
        {
            foreach (var pair in _groups)
                groupsIds.Add(pair.Key);
            return groupIds.Count;
        }
    }

    //todo 그룹 객체도 풀링을할까나?

    /// <summary>
    /// </summary>
    public LinkId CreateGroup(LinkId[] members = null, GroupOptions options = null, byte[] userData = null, LinkId alreadyAssignedLinkId = LinkId.None)
    {
        CheckServerStarted("CreateGroup");

        options ??= new RtGroupOptions();

        lock (_mainLock)
        {
            LinkId groupId = _linkIdAllocator.Allocate(HeartbeatTime, alreadyAssignedLinkId);

            var newGroup = new RtGroup(this, groupId, options);
            _groups.Add(groupId, newGroup);

            if (members != null)
            {
                foreach (var memberId in members)
                {
                    JoinGroup(groupId, memberId, userData);
                }
            }
        }

        return groupId;
    }

    /// <summary>
    /// </summary>
    public bool DestroyGroup(LinkId groupId)
    {
        lock (_mainLock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
                return false;

            var members = new List<LinkId>(group.Members);
            foreach (var memberId in members)
                LeaveGroup(groupId, memberId);

            _groups.Remove(groupId);
            _linkIdAllocator.Free(groupId, HeartbeatTime);
            group.GroupId = LinkId.None;
            return true;
        }
    }

    /// <summary>
    /// </summary>
    public bool JoinGroup(LinkId groupId, LinkId memberId, byte[] userData = null)
    {
        userData ??= Array.Empty<byte>();

        lock (_mainLock)
        {
            if (!_groups.TryGetValue(groupId, out var group))
                return false;

            var member = GetClient(memberId);
            if (member == null)
                return false;

            if (!group.Join(member))
                return false;

            foreach (var m in group.Members)
                _proxyS2C.GroupMemberJoined(m, (int)memberID, (int)groupID, group.MemberCount, userData);

            foreach (var m in group.Members)
            {
                if (m != memberId)
                    _proxyS2C.GroupMemberJoined(memberId, (int)m, (int)groupId, group.MemberCount, userData);
            }
        }
    }

    /// <summary>
    /// </summary>
    public bool LeaveFromJoinedGroups(LinkId clientId)
    {
        lock (_mainLock)
        {
            var client = GetClient(clientId);
            if (client == null)
                return false;

            var clientInfo = client.GetInfo();
            foreach (var groupId in clientInfo.JoinedGroups)
                LeaveGroup(groupId, clientId);

            return true;
        }
    }

    #endregion
