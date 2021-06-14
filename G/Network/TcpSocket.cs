//todo 전체적인 락 체계는 확인하도록 하자.
//최소한의 핑 메시지도 여기서 처리하자.
//핑 메시지를 보낼때 ack도 같이 보내주어서 상대측에서 메시지가 쌓이지 않도록 해줌.
//그럼에도 불구하고 송신 메시지가 너무 많이 쌓이게 되면 연결을 강제로 끊어줌.
//todo 통계 데이터를 관리하도록 하자.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using G.Util;
using System.Collections.Generic;
using System.Linq;
using G.Network.Messaging;
using G.Util.Compression;
using MessagePack;
using PlayTogether;
using PlayTogetherSocket;
using Renci.SshNet.Messages;

namespace G.Network
{
    public abstract class TcpSocket
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private static long _instanceIdSeed = 0;
        public long InstanceId { get; private set; }

        public long userUID;

        public long LastRecvTime => Transport.LastRecvTime;
        public long LastSendTime => Transport.LastSendTime;

        //todo 보통 외부에서 설정해서 쓰곤하니까 관계를 잘 정비할 필요가 있겠다.
        public KeyChain KeyChain { get; private set; } = new KeyChain();

        public IPAddress Host;
        public int Port;
        public IPEndPoint EndPoint;

        public IPEndPoint RemoteEndPoint { get; private set; }

        public bool IsConnected => Transport.IsConnected || IsSuspended;
        public bool IsSuspended => SuspendedTime > 0;
        public bool IsSocketConnected => Transport.IsConnected;

        internal TcpTransport Transport;

        #region Reliable Session

        internal uint? LastRecvSeq;
        internal uint? LastSentAck;
        internal uint NextMessageSeq;

        //public long SessionId { get; internal set; }
        public long SessionId;

        internal readonly Queue<OutgoingMessage> _sent = new Queue<OutgoingMessage>();
        internal float DelayedSendAckInterval = 0f;//2.5f; //todo 옵션에서 가져올 수 있도록 하자.

        public long SuspendedTime { get; set; }
        //protected TaskTimer DelayedSendAckTimer;

        //protected SemaphoreSlim _semaphoreConn = new SemaphoreSlim(1, 1);

        // These can be accessed by TcpServer
        internal TcpServer Server;

        private int _wasDisconnectCalled;

        //todo 상태 관리로 처리하는게 바람직해보임!
        private bool _waitForInitialAck = false;

        // 보내기 작업 관련한 락
        internal object _sendingLock = new object();

        //유실이 발생하지 않으려면 여기에 보관했다가 처리해야함.
        internal object _messagesLock = new object();
        internal Queue<IncomingMessage> _messages = new Queue<IncomingMessage>();
        //todo 여기에 따로두지 말고, 전송 계층내에서 이중시키는게 좋지 않을까?

        protected TcpSocket()
        {
            InstanceId = Interlocked.Increment(ref _instanceIdSeed);

            ResetReliableSession();
        }

        protected TcpSocket(string host, int port) : this()
        {
            SetHostPort(host, port);
        }

        protected TcpSocket(IPAddress host, int port) : this()
        {
            SetHostPort(host, port);
        }

        public void DisableReconnecting()
        {
            if (Transport != null)
            {
                Transport.DisableReconnecting = true;
            }
        }

        public void EnableReconnecting()
        {
            if (Transport != null)
            {
                Transport.DisableReconnecting = false;
            }
        }

        private void SetHostPort(string host, int port)
        {
            if (IPAddress.TryParse(host, out var ipAddress))
            {
                SetHostPort(ipAddress, port);
            }
            else
            {
                SetHostPort(Dns.GetHostAddresses(host)[0], port);
            }
        }

        private void SetHostPort(IPAddress host, int port)
        {
            Host = host;
            Port = port;
            EndPoint = new IPEndPoint(host, port);
        }

        public void SetKey(KeyIndex keyIndex)
        {
            KeyChain.Set(keyIndex);
        }

        public void SetKey(KeyIndex keyIndex, uint[] key)
        {
            KeyChain.Set(keyIndex, key);
        }

        public void SetKey(KeyIndex keyIndex, string base62Key)
        {
            KeyChain.Set(keyIndex, base62Key);
        }

        internal async Task InitializeForFirstAsync()
        {
            await InitializeCommonAsync();

            ResetReliableSession();

            lock (_messagesLock)
            {
                while (_messages.Count > 0)
                {
                    _messages.Dequeue().Return();
                }
            }
        }

        internal async Task InitializeForReconnectAsync()
        {
            await InitializeCommonAsync();
        }

        internal async Task InitializeCommonAsync()
        {
            //Server = null;

            userUID = 0;

            SuspendedTime = 0;
            _wasDisconnectCalled = 0;
            _waitForInitialAck = false;

            RemoteEndPoint = (IPEndPoint)Transport._socket.RemoteEndPoint;
        }

        internal void OnAuthenticated()
        {
            SendInitialAck();

            Transport.SendAllUnsentMessages();
        }

        protected virtual async Task<bool> ConnectAsync()
        {
            Transport = new TcpTransport();
            Transport.Owner = this;

            return await Transport.ConnectAsync(Host, Port);
        }

        public virtual async Task<bool> ConnectAsync(string host, int port)
        {
            SetHostPort(host, port);
            return await ConnectAsync();
        }

        public virtual async Task<bool> ConnectAsync(IPAddress host, int port)
        {
            SetHostPort(host, port);
            return await ConnectAsync();
        }

        public async Task<bool> DisconnectAsync(DisconnectReason disconnectReason)
        {
            bool shouldHardClose =
                    disconnectReason == DisconnectReason.ByLocal ||
                    disconnectReason == DisconnectReason.Replace ||
                    disconnectReason == DisconnectReason.GlobalSessionExpired;

            if (shouldHardClose)
            {
                ReturnMessagesToPool();

                // 이미 콜백했으면 재콜백 금지.
                var wasDisconnectCalled = _wasDisconnectCalled;

                if (wasDisconnectCalled != 0 ||
                    Interlocked.CompareExchange(ref _wasDisconnectCalled, 1, wasDisconnectCalled) != wasDisconnectCalled)
                {
                    return true;
                }
            }

            await Transport.DisconnectAsync(disconnectReason);

            return true;
        }

        public virtual async Task OnConnectAsync()
        {
            // Do nothing by default
        }

        public virtual async Task OnConnectErrorAsync(int error)
        {
            // Do nothing by default
        }

        public virtual async Task OnDisconnectAsync(DisconnectReason disconnectReason)
        {
            bool shouldHardClose =
                    disconnectReason == DisconnectReason.ByLocal ||
                    disconnectReason == DisconnectReason.Replace ||
                    disconnectReason == DisconnectReason.GlobalSessionExpired;

            if (shouldHardClose)
            {
                //@fixme
                // 수신된 메시지도 버린다.
                // 하지만 처리가 누락이 될 수 있으므로, 모두 처리한다음에 소진하도록 하는게 안전하다.
                lock (_messagesLock)
                {
                    while (_messages.Count > 0)
                    {
                        _messages.Dequeue().Return();
                    }
                }

                ReturnMessagesToPool();

                SuspendedTime = 0;
            }
        }

        //@fixme
        // 보낼때 이미 메시지 타입을 알수 있을텐데.
        // 그걸 넣을 수 있도록하고, 이름을 각가 핸들러에서 resolve할 수 있도록 하자.
        public virtual async Task<bool> OnProcessAsync(ReadOnlyMemory<byte> memory)
        {
            // Do nothing by default
            return true;
        }


        #region Statistics
        public int __close;

        public int __sends1;
        public int __recvs1;
        public int __sends0;
        public int __recvs0;

        public int __sendc1;
        public int __recvc1;
        public int __sendc0;
        public int __recvc0;

        public int __sends;
        public int __recvs;
        public int __sendc;
        public int __recvc;

        public void UpdateStat()
        {
            int sends = __sends1;
            int recvs = __recvs1;

            __sends = sends - __sends0;
            __recvs = recvs - __recvs0;

            __sends0 = sends;
            __recvs0 = recvs;

            int sendc = __sendc1;
            int recvc = __recvc1;

            __sendc = sendc - __sendc0;
            __recvc = recvc - __recvc0;

            __sendc0 = sendc;
            __recvc0 = recvc;
        }
        #endregion


        internal void OnAckReceived(uint ack)
        {
            _logger.Debug($"[!] Ack Received: {ack}");

            lock (_sendingLock)
            {
                if (_sent.Count > 0)
                {
                    _logger.Debug($"Purge SentMessages: {_sent.Count}");

                    // Among the messages sent to the other party in storage,
                    // the messages normally received by the other party are removed.
                    while (_sent.Count > 0)
                    {
                        var m = _sent.Peek();

                        if (SeqNumberHelper32.Less(m.Seq.Value, ack))
                        {
                            _logger.Debug($"...Purge sent message: Message={m}");

                            // Dequeue and return into pool.
                            _sent.Dequeue().Return();
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                //todo 상태로 처리하도록 하자.

                // In the reconnection situation, immediately after authentication,
                // it should be in a state of waiting for the other party's ack.
                if (_waitForInitialAck)
                {
                    //_logger.Debug($"...WaitForInitialAck: Ack={ack}");

                    //todo 상태를 전환하도록 하자.
                    _waitForInitialAck = false;

                    if (_sent.Count > 0)
                    {
                        //_logger.Debug($"InitialAck: Ack={ack}, SentQueue={_sent.Count}");

                        foreach (var m in _sent)
                        {
                            if (SeqNumberHelper32.LessOrEqual(ack, m.Seq.Value))
                            {
                                // Resend a message that has already been sent but has not been received by the other party
                                _logger.Debug($"...Resend a message: Message={m}");

                                Transport.SendMessage(m);
                            }
                            else
                            {
                                //_logger.Error($"...Wrong sequence number: {m.Seq.Value}");
                            }
                        }
                    }
                }
            }
        }

        internal bool OnSeqReceived(uint seq)
        {
            _logger.Debug($"[!] Seq Received: {seq}");

            lock (_sendingLock)
            {
                if (LastRecvSeq.HasValue)
                {
                    // 원하는 메시지 번호보다 미래의 메시지가 도착했음.
                    // 버그일까?
                    if (!SeqNumberHelper32.Less(LastRecvSeq.Value, seq))
                    {
                        _logger.Warn($"...Last sequence number is {LastRecvSeq.Value} but {seq} received. Skipping messages.");
                        return false;
                    }

                    // If the message sequence number is incorrect, disconnect the connection.
                    // (preventing packet replay attacks)
                    if (seq != LastRecvSeq.Value + 1)
                    {
                        _logger.Error($"...Received wrong sequence number {seq}. {LastRecvSeq.Value + 1} expected. but {seq} is received.");

                        // Will be disconnected.
                        return false;
                    }
                }

                // Store last received message sequence number.
                LastRecvSeq = seq;

                // 일정주기마다 Ack를 보내는 경우가 아니라면 Seq를 받을때마다, 매번 Ack를 보내도록 한다.
                if (DelayedSendAckInterval <= 0f)
                {
                    SendAck(LastRecvSeq.Value + 1);
                }

                return true;
            }
        }

        //todo 타이머에 의해서 보내줘야, 상대측에서 재전송을 위해서 보낸 메시지를 다량으로 쌓아놓지 않고 비울 수 있다.

        private void SendAck(uint ack, bool sendingFirst = false)
        {
            // 연결이 안되어 있는 상태에서는 어짜피 보내지 못함.
            if (!IsConnected)
            {
                return;
            }

            // 아직 메시지를 한번도 받지 않았다면, ack를 보낼수가 없음.
            if (LastRecvSeq == null)
            {
                return;
            }

            _logger.Debug($"SendAck({ack})");

            var message = OutgoingMessage.Rent();
            message.MessageType = MessageType.None;
            message.Ack = ack;
            Transport.SendMessage(message, sendingFirst);

            LastSentAck = ack;
        }

        //todo 서버에서 메시지를 보내기만 할 경우, 이걸 가끔식 해줘야 메시지가 안쌓이게 됨.

        // 타이머에서 일정 간격마다 호출되어야함.
        // 다른 메시지에 묻어가므로, 주기적으로 호출하지 않아도 문제는 없을듯 싶으나,
        // 주기적으로 보내는 메시지가 하나도 없는 경우에는 필요할 수 있음.
        private void OnDelayedSendAck()
        {
            // 아직 메시지를 한번도 받지 않았다면, ack를 보낼수가 없음.
            if (LastRecvSeq == null)
            {
                return;
            }

            // Send ack if there is no recently sent ack or less than received seq.
            if (LastSentAck == null ||
                SeqNumberHelper32.Less(LastSentAck.Value, LastRecvSeq.Value + 1))
            {
                SendAck(LastRecvSeq.Value + 1);
            }
        }

        // 재접속일 경우에는 단순히 Transport만 교체해줌.
        // 어짜피 Transport는 새로 생성한거라 꼬일 염려없음.
        internal async Task ReplaceTransportAsync(TcpTransport transport)
        {
            // 락이슈가 있을듯 하다.
            // Receive 스레드뿐만 아니라 Send 스레드에서도 Owner를 액세스할 수 있으므로 대비를 해야함.
            if (Transport != null)
            {
                Transport.Owner = null;
                await Transport.DisconnectAsync(DisconnectReason.Replace, true); // Do not call callback

                // 내부 펜딩 메시지 옮겨주기.
                Transport.MigratePendingMessages(transport);
            }

            Transport = transport;
            Transport.Owner = this;
            Transport.DisableReconnecting = false;

            //await InitializeAsync(Transport._socket, true);
            await InitializeForReconnectAsync();

            // Transport에서 암호화키 가져오기.
            KeyChain = Transport.KeyChain.Clone();

            //추가로 해야할 작업들이 있을까?
            //await OnTransportResumedAsync();

            //todo 상태를 바꿔주는 형태로 처리하자.

            var message = OutgoingMessage.Rent();
            message.MessageType = MessageType.HandshakeRes;
            //message.KeyIndex = KeyIndex.Common;
            message.SessionId = this.SessionId;
            message.RemoteEncryptionKeyIndex = KeyIndex.Remote; //with remote key

            //_logger.Debug($"message.SessionId={message.SessionId}");

            //요청만 하고 실제 보내기는 별도로 처리하는게 바람직함.
            Transport.SendMessage(message);

            _waitForInitialAck = true;
        }

        // 최초 접속일 경우에도 딱히 추가로 처리할 작업이 없음.
        internal async Task AssignNewTransportAsync(TcpTransport transport)
        {
            // 락이슈가 있을듯 하다.
            // Receive 스레드뿐만 아니라 Send 스레드에서도 Owner를 액세스할 수 있으므로 대비를 해야함.
            Transport = transport;
            Transport.Owner = this;
            Transport.DisableReconnecting = false;

            await InitializeForFirstAsync();

            // Transport에서 암호화키 가져오기.
            KeyChain = Transport.KeyChain.Clone();

            //await OnTransportAttachedAsync();

            var message = OutgoingMessage.Rent();
            message.MessageType = MessageType.HandshakeRes;

            //message.KeyIndex = KeyIndex.Common;
            message.SessionId = this.SessionId;
            message.RemoteEncryptionKeyIndex = KeyIndex.Remote; //with remote key

            //요청만 하고 실제 보내기는 별도로 처리하는게 바람직함.
            Transport.SendMessage(message);

            await OnConnectAsync();
        }

        private void ResetReliableSession()
        {
            var rnd = new System.Random();

            lock (_sendingLock)
            {
                //SessionId = 0;

                LastRecvSeq = null;
                LastSentAck = null;
                NextMessageSeq = (uint)rnd.Next() + (uint)rnd.Next();

                //todo 이건 닫히는 시점에서 설정하는게 맞을듯 싶다.
                SuspendedTime = 0;

                _waitForInitialAck = false;
            }

            ReturnMessagesToPool();
        }

        internal uint GetNextMessageSeq()
        {
            return NextMessageSeq++;
        }

        internal void ResetSessionId()
        {
            Interlocked.Exchange(ref SessionId, 0);
        }

        private void ReturnMessagesToPool()
        {
            // Return _sent to pool.
            lock (_sendingLock)
            {
                while (_sent.Count > 0)
                {
                    _sent.Dequeue().Return();
                }
            }
        }

        public void SendPacket(BaseProtocol protocol, uint uniqueKey = 0, CompressionType compressionType = CompressionType.None)
        {
            Transport.SendPacket(protocol, uniqueKey, compressionType);
        }

        protected virtual KeyIndex ResolveKeyIndex(BaseProtocol protocol)
        {
            return KeyIndex.None;
        }

        protected virtual Type GetBaseProtocolType()
        {
            return typeof(object);
        }

        public virtual void SerializeProtocol(Stream stream, object protocol)
        {
            MessagePackSerializer.Serialize(stream, protocol);
        }

        // Requests the other party to resend a message that the receiver did not receive by sending an ack at random.
        private void SendInitialAck()
        {
            lock (_sendingLock)
            {
                if (LastRecvSeq.HasValue)
                {
                    _logger.Debug($"Send initial ack: Ack={LastRecvSeq.Value + 1}");

                    SendAck(LastRecvSeq.Value + 1, true);
                }
            }
        }

        internal KeyIndex ResolveKeyIndex(ProtocolId protocolId)
        {
            KeyIndex keyIndex = KeyIndex.Remote;
            switch (protocolId)
            {
                case ProtocolId.AliveQ:
                case ProtocolId.AliveA:
                case ProtocolId.SyncUserQ:
                case ProtocolId.ActionUserQ:
                case ProtocolId.SyncJumpQ:
                case ProtocolId.ChatToAllQ:
                    keyIndex = KeyIndex.None;
                    break;

                case ProtocolId.AuthQ:
                case ProtocolId.ReconnectQ:
                    keyIndex = KeyIndex.Common;
                    break;

                case ProtocolId.OnlineQ:
                case ProtocolId.CityEnterQ:
                case ProtocolId.FollowEnterA:
                case ProtocolId.FollowEnterQ:
                case ProtocolId.SchoolEnterQ:
                case ProtocolId.SchoolLeaveQ:
                case ProtocolId.CityLeaveQ:
                case ProtocolId.HomeEnterReservationQ:
                case ProtocolId.HomeEnterQ:
                case ProtocolId.HomePartyEnterReservationQ:
                case ProtocolId.HomePartyEnterQ:
                case ProtocolId.NowTimeQ:
                case ProtocolId.NowLatencyQ:
                case ProtocolId.GameJoinQ:
                case ProtocolId.MailListQ:
                case ProtocolId.MatchQuitQ:
                case ProtocolId.GameQuitQ:
                    break;

                default:
                    break;
            }

            return keyIndex;
        }

        #endregion
    }
}
