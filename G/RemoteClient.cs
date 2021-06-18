namespace Lane.Realtime.Server.Internal
{
    internal class RemoteClient : ListNode<RemoteClient>, IHoldingCounter
    {
        #region Holding counter
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
        internal CryptoKey _sessionChaCha20;
        #endregion


        internal ClosingTicket ClosingTicket { get; set; }
        internal SoftClosingRequestedTime { get; set; }
        internal bool PurgeRequestedTime { get; set; }
        internal byte[] ShutdownComment { get; set; }
        internal string DisposeCaller { get; set; }
        internal long LastTcpStreamReceivedTime { get; set; }
        internal HandshakeState HandshakeState { get; set; }
        internal readonly RtServer _server;

        public RemoteClient(RtServer server, TcpTransport transport)
        {
            _server = PreValidations.CheckNotNull(server);

            LinkId = LinkId.None;
            JoinedGroups = new List<LinkId>();
            CreatedTime = server.HeartbeatTime;
            Transport = PreValidations.CheckNotNull(transport);
            LastTcpStreamReceivedTime = _server.HeartbeatTime;
        }

        public RemoteClientInfo GetInfo()
        {
            return new RemoteClientInfo
            {
                LinkId = linkId,
                ExternalAddress = ExternalAddress,
                InternalAddress = InternalAddress,
                JoinedGroups = JoinedGroups,
                LinkTag = LinkTag,
            }
        }

        internal void HardClose(int reason,
                        string detail = "",
                        byte[] comment = null,
                        string calledWhere = "",
                        SocketError socketError = SocketError.Success)
        {
            _server.HardCloseClient(this, reason, detail, comment, calledWhere, socketError);
        }

        internal bool IsSendRequested
        {
            get
            {
                lock (_server._tcpSendRequestQueueLock)
                    return IsLinked;
            }
        }

        public void SendMessage(Message message)
        {
            Transport.SendMessage(message);

            lock (_server._tcpSendRequestQueueLock)
            {
                if (!IsLinked)
                {
                    _server._tcpSendRequestQueue.Append(this);
                }
            }
        }

        public void SendMessageNow(Message message)
        {
            lock (_server._tcpSendRequestQueueLock)
            {
                if (IsLinked)
                {
                    UnlinkSelf();
                }
            }

            Transport.SendNow(message);
        }
    }
}
