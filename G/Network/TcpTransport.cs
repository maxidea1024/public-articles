//todo Reliable이 필요없는 상황에 대해서 처리가 가능하도록 하자.
//상태관리를 통한 흐름체크만 제대로 된다면, 오류 검증은 어렵지 않게 될듯함.
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
    internal class TcpTransport
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private const int SendBufferMax = 32768;
        private const int UnitBufferSize = 65536;

        public long CreatedTime { get; internal set; }
        public long LastRecvTime { get; internal set; }
        public long LastSendTime { get; internal set; }

        public IPEndPoint RemoteEndPoint { get; internal set; }

        private CancellationTokenSource _cts { get; set; }

        internal Socket _socket;

        private KeyChain _keyChain = new KeyChain();
        internal KeyChain KeyChain => _keyChain;

        private SemaphoreSlim _semaphoreConn = new SemaphoreSlim(1, 1);
        private object _socketLock = new object();
        private object _sendingLock = new object();
        private object _receivingLock = new object();

        private byte[] _receiveBuffer = new byte[UnitBufferSize];
        private int _receivedSize = 0;
        private int _nextDecodingOffset = 0;

        private object _messagesLock = new object();
        private Queue<IncomingMessage> _messages = new Queue<IncomingMessage>();

        private Queue<OutgoingMessage> _first = new Queue<OutgoingMessage>();
        private Queue<OutgoingMessage> _pending = new Queue<OutgoingMessage>();
        private Queue<OutgoingMessage> _sending = new Queue<OutgoingMessage>();

        private readonly List<ArraySegment<byte>> _segmentsForSend = new List<ArraySegment<byte>>();

        internal readonly Queue<OutgoingMessage> _unsent = new Queue<OutgoingMessage>();

        internal TcpServer Server { get; set; }
        internal TcpSocket Owner { get; set; }

        private bool IsEstablished = false;

        public bool IsConnected
        {
            get
            {
                try { return _socket != null && _socket.Connected; }
                catch (Exception) { return false; }
            }
        }

        private bool IsSendable
        {
            get
            {
                lock (_sendingLock)
                {
                    return _sending.Count == 0;
                }
            }
        }

        internal bool DisableReconnecting { get; set; }
        private int _wasDisconnectCalled = 0;

        internal TcpTransport()
        {
            _keyChain.Reset();
        }

        internal void SetCommonKey(uint[] key)
        {
            _keyChain.Set(KeyIndex.Common, key);
        }

        internal void SetCommonKey(string base62Key)
        {
            _keyChain.Set(KeyIndex.Common, base62Key);
        }

        internal async Task InitializeAsync(Socket socket)
        {
            try
            {
                var now = SystemClock.Milliseconds;

                CreatedTime = now;

                _cts = new CancellationTokenSource();

                _socket = socket;
                _socket.NoDelay = true;
                _socket.LingerState = new LingerOption(true, 0);

                //todo common은 리셋하지 말자!
                //_keyChain.Reset();
                _keyChain.Set(KeyIndex.Remote);

                lock (_receivingLock)
                {
                    _receivedSize = 0;
                    _nextDecodingOffset = 0;
                }

                LastRecvTime = now;
                LastSendTime = now;

                //todo 이건 함부로 지우면 안됨.
                //명시적으로 세션을 종료했을 경우에 한해서만 지움.
                //그런데, 이미 Owner로 이주시켰으므로 제거해도 무방할 수 있음.
                //lock (_messagesLock)
                //{
                //    _messages.Clear();
                //}

                //얘네는 비워주고, 플로우 상에서 인증후에 unsent 메시지들만 일괄전송하면 됨.
                //lock (_sendingLock)
                //{
                //    _first.Clear();
                //    _pending.Clear();
                //    _segmentsForSend.Clear();
                //}

                // 접속하자마자 세션 성립 요청을 서버에 보냄.
                // 초송신 메시지 처리를 하는게 좋을듯함
                if (Server == null) // 클라이언트 커넥션으로만 사용하는 경우에는 요청!
                {
                    var message = OutgoingMessage.Rent();
                    message.MessageType = MessageType.HandshakeReq;
                    message.SessionId = 0;
                    SendMessage(message, true); // sending First
                }

                // Caching remote endpoint.
                RemoteEndPoint = (IPEndPoint)_socket.RemoteEndPoint;

                try
                {
                    await OnConnectAsync();
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }

                // If there is a message to send, start sending the message.
                CheckPendingMessages(true);

                // Start receiving and processing.
                _ = Task.Run(async () => await RunToReceiveAsync());
            }
            catch (Exception)
            {
                try
                {
                    await OnConnectErrorAsync(-1);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }

                throw;
            }
        }

        internal async Task<bool> ConnectAsync(IPAddress host, int port)
        {
            try
            {
                //await DisconnectAsync(DisconnectReason.ByLocal);

                //연결을 중복해서 요청하지 못하도록함.
                //await _semaphoreConn.WaitAsync();

                var socket = new Socket(host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                await socket.ConnectAsync(host, port);

                await InitializeAsync(socket);

                CheckPendingMessages(true);

                return true;
            }
            catch (Exception e)
            {
                // _logger.Error(e);
                // if 0 == Id, then no Release..
                // connect 요청 할때 에러가 발생한 경우..

                //if (_socket == null && 0 == _semaphoreConn.CurrentCount)
                //{
                //  _semaphoreConn.Release();
                //  _logger.Debug("tried _semaphoreConn.Release.. Count[ {0} ]", _semaphoreConn.CurrentCount);
                //}

                _logger.Error(e);

                try
                {
                    await OnConnectErrorAsync(-1);
                }
                catch (Exception e2)
                {
                    _logger.Error(e2);
                }

                await DisconnectAsync(DisconnectReason.ConnectFailure, true);

                return false;
            }
        }

        private async Task RunToReceiveAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    int receivedBytes = await _socket.ReceiveAsync(
                        _receiveBuffer.AsMemory(_receivedSize, _receiveBuffer.Length - _receivedSize),
                        SocketFlags.None);

                    if (receivedBytes <= 0)
                    {
                        // Disconnected from remote.
                        throw new SocketException();
                    }

                    lock (_receivingLock)
                    {
                        LastRecvTime = SystemClock.Milliseconds;

                        _receivedSize += receivedBytes;

                        ParseMessages();

                        CheckReceiveBuffer();
                    }

                    await ProcessAsync();
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.Success)
                {
                    Log($"RunToReceiveAsync: Disconnected from remote.");
                }
                else
                {
                    Log($"RunToReceiveAsync: SocketException SocketErrorCode: {e.SocketErrorCode}");
                }

                await DisconnectAsync(DisconnectReason.RecvFailure);
            }
            catch (OperationCanceledException)
            {
                Log($"RunToReceiveAsync. OperationCanceledException.");

                await DisconnectAsync(DisconnectReason.RecvFailure);
            }
            catch (Exception e)
            {
                if (e is ObjectDisposedException || e is NullReferenceException)
                {
                    return;
                }

                Log($"RunToReceiveAsync. Exception.  e: {e}");
                await DisconnectAsync(DisconnectReason.RecvFailure);
            }
        }

        private bool ParseMessages()
        {
            lock (_messagesLock)
            {
                while (_nextDecodingOffset < _receivedSize)
                {
                    IncomingMessage incomingMessage = null;

                    var data = new Memory<byte>(_receiveBuffer, _nextDecodingOffset, _receivedSize - _nextDecodingOffset);
                    int length = IncomingMessage.Parse(data, _keyChain, out incomingMessage);

                    if (length <= 0)
                    {
                        break;
                    }

                    if (Owner != null)
                    {
                        lock (Owner._messagesLock)
                        {
                            Owner._messages.Enqueue(incomingMessage);
                        }
                    }
                    else
                    {
                        _messages.Enqueue(incomingMessage);
                    }

                    _nextDecodingOffset += length;
                }
            }

            return true;
        }

        private void CheckReceiveBuffer(int additionalSize = 0)
        {
            int remainingSize = _receiveBuffer.Length - (_receivedSize + additionalSize);
            if (remainingSize > 0)
            {
                return;
            }

            // Calculate new buffer size.
            int retainSize = _receivedSize - _nextDecodingOffset + additionalSize;
            int newSize = _receiveBuffer.Length;
            while (newSize <= retainSize)
            {
                newSize += UnitBufferSize;
            }

            byte[] newBuffer = new byte[newSize];

            // If there are spaces that can be collected, compact it first.
            // Otherwise, increase the receiving buffer size.
            if (_nextDecodingOffset > 0)
            {
                Log($"Compacting the receive buffer to save {_nextDecodingOffset} bytes.");

                Buffer.BlockCopy(_receiveBuffer, _nextDecodingOffset, newBuffer, 0, _receivedSize - _nextDecodingOffset);
                _receiveBuffer = newBuffer;
                _receivedSize -= _nextDecodingOffset;
                _nextDecodingOffset = 0;
            }
            else
            {
                Log($"Increasing the receive buffer to {newSize} bytes.");

                Buffer.BlockCopy(_receiveBuffer, 0, newBuffer, 0, _receivedSize);
                _receiveBuffer = newBuffer;
            }
        }

        protected virtual async Task OnConnectAsync()
        {
            if (Owner != null)
            {
                await Owner.OnConnectAsync();
            }
        }

        protected virtual async Task OnConnectErrorAsync(int error)
        {
            if (Owner != null)
            {
                await Owner.OnConnectErrorAsync(error);
            }
        }

        protected virtual async Task OnDisconnectAsync(DisconnectReason disconnectReason)
        {
            lock (_sendingLock)
            {
                while (_unsent.Count > 0)
                {
                    _unsent.Dequeue().Return();
                }
            }

            if (Owner != null)
            {
                if (Server != null)
                {
                    await Server.RemoveFromSuspendedAsync((TcpRemote)Owner);
                    await Owner.OnDisconnectAsync(disconnectReason);
                }
                else
                {
                    await Owner.OnDisconnectAsync(disconnectReason);
                }
            }
        }

        protected virtual async Task<bool> OnProcessAsync(ReadOnlyMemory<byte> memory)
        {
            if (Owner != null)
            {
                return await Owner.OnProcessAsync(memory);
            }

            return true;
        }

        public void SendMessage(OutgoingMessage message, bool sendingFirst = false)
        {
            string bodyTypeName = message.Body != null ? $":{message.Body.GetType().ToString()}" : "";
            _logger.Debug($"{RemoteEndPoint}.SendMessage: MessageType={message.MessageType}{bodyTypeName}");

            if (!sendingFirst && !IsEstablished)
            {
                lock (_sendingLock)
                {
                    _unsent.Enqueue(message);
                }

                return;
            }

            try
            {
                lock (_sendingLock)
                {
                    if (sendingFirst)
                    {
                        _first.Enqueue(message);
                    }
                    else
                    {
                        _pending.Enqueue(message);
                    }

                    if (IsConnected && IsSendable)
                    {
                        SendPendingMessages(true);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);

                //todo Should disconnect here?
            }
        }

        // Send the requested messages after the session is established.
        internal void SendAllUnsentMessages()
        {
            lock (_sendingLock)
            {
                if (_unsent.Count > 0)
                {
                    Log($"Send all unsent messages: Count={_unsent.Count}");

                    while (_unsent.Count > 0)
                    {
                        var m = _unsent.Dequeue();
                        SendMessage(m);
                    }
                }
            }
        }

        // To prevent message loss when transport is replaced
        // Migrate delayed messages from the old transport to the new transport.
        internal void MigratePendingMessages(TcpTransport target)
        {
            lock (_sendingLock)
            {
                while (_first.Count > 0)
                {
                    target._first.Enqueue(_first.Dequeue());
                }

                while (_pending.Count > 0)
                {
                    target._pending.Enqueue(_pending.Dequeue());
                }
            }
        }

        // Sends all pendeing messages to wire.
        private void SendPendingMessages(bool shouldBeginSend)
        {
            try
            {
                lock (_sendingLock)
                {
                    // If there is no message to send or there is no message to send, return immediately.
                    if (!IsSendable || (_first.Count == 0 && _pending.Count == 0))
                    {
                        return;
                    }

                    // _first가 우선 송신 대상임.
                    var tmp = _sending;
                    if (_first.Count > 0)
                    {
                        _sending = _first;
                        _first = tmp;
                    }
                    else
                    {
                        //세션 ID를 받기도 전에는 메시지를 보낼 수 없는가?
                        //if (SessionId == 0)
                        //{
                        //    return;
                        //}

                        _sending = _pending;
                        _pending = tmp;
                    }

                    foreach (var m in _sending)
                    {
                        if (!m.IsEncoded)
                        {
                            EncodeMessage(m);
                        }
                        else
                        {
                            ReEncodeMessage(m);
                        }
                    }

                    WireSend(shouldBeginSend);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);

                //todo 끊는 플로우로 변경하자.
            }
        }

        private void CheckPendingMessages(bool shouldBeginSend)
        {
            lock (_sendingLock)
            {
                if (IsSendable)
                {
                    SendPendingMessages(shouldBeginSend);
                }
            }
        }

        private void WireSend(bool shouldBeginSend)
        {
            try
            {
                int length = 0;

                lock (_sendingLock)
                {
                    _segmentsForSend.Clear();

                    foreach (var m in _sending)
                    {
                        // With at least one added, if the message length exceeds SendBufferMax, send it next time.
                        if (_segmentsForSend.Count > 0 &&
                            (length + m.SendableHeader.Count + m.SendableBody.Count) > SendBufferMax)
                        {
                            break;
                        }

                        // Add header unconditionally.
                        if (m.SendableHeader.Count > 0)
                        {
                            _segmentsForSend.Add(m.SendableHeader);
                            length += m.SendableHeader.Count;
                        }

                        // Add body (partially)
                        if (m.SendableBody.Count > 0)
                        {
                            if ((length + m.SendableBody.Count) > SendBufferMax)
                            {
                                // Partially
                                int partialSent = SendBufferMax - length;
                                _segmentsForSend.Add(m.SendableBody.Slice(partialSent));
                                length += partialSent;
                                break;
                            }
                            else
                            {
                                // Whole
                                _segmentsForSend.Add(m.SendableBody);
                                length += m.SendableBody.Count;
                            }
                        }
                    }
                }

                // No data to send.
                // Even if we make a request, SendAsync only generates an error, so return immediately.
                if (length == 0)
                {
                    return;
                }

                // Depending on whether I/O processing is requested or not.
                // If it is called inside RunToSendAsync(), it is already in the I/O processing loop,
                // Don't run RunToSendAsync() as a task again.
                if (shouldBeginSend)
                {
                    _ = Task.Run(async () => await RunToSendAsync());
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);

                // Rethrow
                //throw;
            }
        }

        private async Task RunToSendAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_socket == null)
                    {
                        return;
                    }

                    int totalLength = 0;

                    lock (_sendingLock)
                    {
                        foreach (var segment in _segmentsForSend)
                        {
                            totalLength += segment.Count;
                        }
                    }

                    int sent = await _socket.SendAsync(_segmentsForSend, SocketFlags.None);
                    if (sent <= 0)
                    {
                        // Send failure, will be disconnect.
                        _logger.Debug("Socket is closed while sending.");
                        throw new SocketException();
                    }

                    lock (_sendingLock)
                    {
                        if (sent == totalLength)
                        {
                            // When all requested data has been sent.

                            while (_sending.TryDequeue(out var m))
                            {
                                // Reliable messages should be returned to the pool only
                                // when an Ack is received or the connection is completely disconnected.
                                if (!m.Seq.HasValue)
                                {
                                    _logger.Debug($"SENT AND CONSUME message: {m.MessageType}:{m.Body}");

                                    // There is no further reference, so we return it into pool here.
                                    m.Return();
                                }
                                else
                                {
                                    _logger.Debug($"SENT AND KEEP message: {m.MessageType}:{m.Body}");
                                }
                            }
                        }
                        else
                        {
                            // Partially successful sending.

                            while (sent > 0)
                            {
                                if (!_sending.TryPeek(out var m))
                                {
                                    _logger.Error($"Sent {sent} bytes but couldn't find the sending buffer.");
                                    throw new SocketException();
                                }

                                int length = m.SendableHeader.Count + m.SendableBody.Count;
                                if (length <= sent)
                                {
                                    // One message has been sent intact and can be removed.
                                    if (m.SendableHeader.Count == 0)
                                    {
                                        _logger.Debug("Partially sent {sent} bytes. 0 bytes left.");
                                    }

                                    // Reliable messages should be returned to the pool only
                                    // when an Ack is received or the connection is completely disconnected.
                                    if (!m.Seq.HasValue)
                                    {
                                        _logger.Debug($"SENT AND FLUSH MESSAGE: {m.MessageType}:{m.Body}");

                                        // There is no further reference, so we return it into pool here.
                                        m.Return();
                                    }
                                    else
                                    {
                                        _logger.Debug($"SENT AND KEEP MESSAGE: {m.MessageType}:{m.Body}");
                                    }

                                    sent -= length;
                                    _sending.Dequeue();
                                }
                                else
                                {
                                    int offset = sent - m.SendableHeader.Count;

                                    if (m.SendableHeader.Count > 0)
                                    {
                                        m.SendableHeader = ArraySegment<byte>.Empty;
                                    }

                                    m.SendableBody = m.SendableBody.Slice(offset);

                                    _logger.Debug($"Partially sent (sent) bytes. {m.SendableBody.Count} bytes left.");
                                    break;
                                }
                            }

                            if (_sending.Count > 0)
                            {
                                _logger.Debug("{_sending.Count} message(s) left in the sending buffer.");
                            }
                        }

                        if (_sending.Count == 0)
                        {
                            // If there are more messages waiting to be sent, fill in the list of network messages to be sent.
                            // Since I/O has already been issued, only the transmission list is filled.
                            CheckPendingMessages(false);

                            // No more messages to send.
                            if (_sending.Count == 0)
                            {
                                return;
                            }
                        }
                    } // _sendingLock
                }
            }
            catch (ObjectDisposedException)
            {
                _logger.Debug("BeginSend operation has been cancelled.");
            }
            catch (Exception e)
            {
                if (e is ObjectDisposedException || e is NullReferenceException)
                {
                    _logger.Debug("Socket is closed while sending.");
                    return;
                }

                // There is no need to show the old message.
                _logger.Warn(e);

                _ = Task.Run(async () => await DisconnectAsync(DisconnectReason.SendFailure));
            }
        }

        private void Log(string message)
        {
            long sessionId = 0;
            if (Owner != null)
            {
                sessionId = Owner.SessionId;
            }

            _logger.Debug($"[SessionId={sessionId}, Remote={_socket.RemoteEndPoint}] {message}");
        }

        private void EncodeMessage(OutgoingMessage message)
        {
            // seq가 부여되어야하는 경우와 아닌 경우 구분.
            bool needSeq = true;

            if (message.Body == null)
            {
                needSeq = false;

                // common으로 해야할까?
                message.KeyIndex = KeyIndex.None;
            }
            else
            {
                //todo 이건 외부에서 처리하는게 바람직할듯 한데..
                var protocolId = message.Body.RawProtocolId;

                //switch ((ProtocolId)protocolId)
                //{
                //    //case ProtocolId.AliveQ:
                //    case ProtocolId.AliveA:
                //    //case ProtocolId.SyncUserQ:
                //    //case ProtocolId.ActionUserQ:
                //    //case ProtocolId.SyncJumpQ:
                //    //case ProtocolId.ChatToAllQ:
                //        needSeq = false;
                //        break;
                //}

                message.KeyIndex = Owner.ResolveKeyIndex((ProtocolId)protocolId);
            }

            if (needSeq)
            {
                lock (Owner._sendingLock)
                {
                    // Assign sequence number to message.
                    message.Seq = Owner.GetNextMessageSeq();

                    // 메시지를 보낼때 ack를 실어서 보낼 조건이 된다면 첨부하도록 함.
                    if (Owner.LastRecvSeq.HasValue &&
                        (!Owner.LastSentAck.HasValue || SeqNumberHelper32.Less(Owner.LastSentAck.Value, Owner.LastRecvSeq.Value + 1)))
                    {
                        var ack = Owner.LastRecvSeq.Value + 1;
                        message.Ack = ack;

                        // It will be carried in the message, so it is considered to have been sent.
                        Owner.LastSentAck = ack;

                        //Log($"SendAckWithMessage({ack})");
                    }

                    Owner._sent.Enqueue(message);
                }
            }

            if (Owner != null)
            {
                message.Build(Owner.SerializeProtocol, _keyChain);
            }
            else
            {
                message.Build(null, _keyChain); // 아직 Attach 되기전.
            }
        }

        private void ReEncodeMessage(OutgoingMessage message)
        {
            //todo 만약 KeyIndex가 common이면 재 암호화할 필요는 없다.

            message.Rebuild(_keyChain);
        }

        internal void SendPacket(BaseProtocol protocol, uint uniqueKey, CompressionType compressionType)
        {
            var message = OutgoingMessage.Rent();
            message.MessageType = MessageType.User;
            message.CompressionType = compressionType;

            // 이 값이 0이 아니면, 이 값에 해당하는 메시지가 이미 있을 경우 최종 메시지로 대체함.
            // 아직 실제 Collapsing 기능이 구현된건 아님.
            // 보낸 메시지에서 찾아서 업데이트 쳐주는 형태로 처리하면 될듯한데.
            message.UniqueKey = uniqueKey;

            message.Body = protocol;
            SendMessage(message);
        }

        //todo DoubleBufferedQueue를 지원하도록 하자.
        private async Task ProcessAsync()
        {
            IncomingMessage incomingMessage = null;
            var needToDisconnect = false;
            DisconnectReason disconnectReason = DisconnectReason.ByLocal;

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    //todo 구지 이렇게 지저분하게 처리해야할까? Transport와 Owner가 나뉘어져 있어서 발생하는
                    // 부분이긴한데, 차후에 정리가 필요할 수 있다.
                    if (Owner == null)
                    {
                        lock (_messagesLock)
                        {
                            if (_messages.Count == 0)
                            {
                                break;
                            }

                            incomingMessage = _messages.Dequeue();
                        }
                    }
                    else
                    {
                        lock (Owner._messagesLock)
                        {
                            if (Owner._messages.Count == 0)
                            {
                                break;
                            }

                            incomingMessage = Owner._messages.Dequeue();
                        }
                    }

                    // 클라에서 명시적으로 접속해제 요청이 왔을 경우.
                    // 별도의 메시지 핸들러로 처리하자.
                    // 너무 지저분하네.
                    if (incomingMessage.MessageType == MessageType.ShutdownTcp)
                    {
                        // ShutdownTcpAck를 보내고 연결을 끊어준다.
                        // 다보낼때까지 대기해야함..

                        var response = OutgoingMessage.Rent();
                        response.MessageType = MessageType.ShutdownTcpAck;
                        SendMessage(response);

                        //todo 다 보낼때까지 대기해야하나, 당장은 지연을 약간 주는 정도에서 마무리
                        //ClosingTicket 시스템을 구현하면 될듯함.
                        await Task.Delay(2_000);

                        needToDisconnect = true;
                        disconnectReason = DisconnectReason.ByRemote;
                    }

                    //todo SessionId, Seq, Ack, IP등을 출력할 수 있으면 좋을듯..
                    //Log($"ProcessAsync: MessageType={incomingMessage.MessageType}");

                    //todo 아래 일련의 플래그 체크는 상태흐름에 따라서 처리하도록 변경하면 문제 없을듯.

                    if (Server != null)
                    {
                        if (incomingMessage.MessageType == MessageType.HandshakeReq && !IsEstablished)
                        {
                            //todo RemoteEndPoint를 키로해서 중복 요청하면 끊어버리는 동작을 하나 넣자.
                            //todo Unmature Candidates를 expire시키자.

                            // 기존 suspended 커넥션중에 연결이 있으면 제거
                            // 중복 접속이면?

                            //todo 재접속 상황에서는 하면 안되는데?
                            //bool overrideExistingRemote = false;
                            //if (incomingMessage.UserSuid != 0)
                            //{
                            //    overrideExistingRemote = await Server.RemoveRemoteByUserSuid(incomingMessage.UserSuid);
                            //}

                            IsEstablished = true;

                            await Server.RemoveCandidateAsync(this);

                            TcpRemote remote = null;

                            if (incomingMessage.SessionId != 0)
                            {
                                Log($"Starts Reconnecting: SessionId={incomingMessage.SessionId}, RemoteAddress={_socket.RemoteEndPoint}");

                                // 재연결 요청

                                //todo 전역 세션이 expired된 상태라면, 오류코드를 달리 주는것도 좋을듯..

                                remote = await Server.FindRemoteForRecoverAsync(incomingMessage.SessionId);
                                if (remote == null)
                                {
                                    _logger.Debug($"SuspendedSession is expired: SessionId={incomingMessage.SessionId}");

                                    var message = OutgoingMessage.Rent();
                                    message.MessageType = MessageType.HandshakeRes;
                                    message.ResultCode = NetworkResult.ContextExpired;
                                    SendMessage(message);

                                    //@todo 실제로 메시지를 모두 전송할때까지 대기해주도록 하자.
                                    await Task.Delay(2000);
                                    await DisconnectAsync();
                                    return;
                                }
                                else
                                {
                                    // 기존 컨텍스트를 찾았으므로, 기존 컨텍스트의 Transport를 이 Transport로 변경해준다.
                                    // 만약, 기존 컨텍스트안에 이미 Transport가 있다면,
                                    // 기존 Transport는 닫아주어야함.

                                    await remote.ReplaceTransportAsync(this);
                                }

                                //todo 내부에 남아있는 메시지를 모두 유저 레벨로 옮겨주는게 좋을듯함.
                                //todo 코드 정리는 차후에 하자.

                                // 타이밍상 ParseMessages()에서 흘러 내려왔을테니까 여기서 이렇게 처리해줘야 유실이 발생안함.
                                lock (_messagesLock)
                                {
                                    lock (Owner._messagesLock)
                                    {
                                        foreach (var m in _messages)
                                        {
                                            Owner._messages.Enqueue(m);
                                        }
                                        _messages.Clear();
                                    }
                                }
                            }
                            else
                            {
                                Log($"Starts First Connecting: RemoteAddress={_socket.RemoteEndPoint}");

                                // 재접속이 아닌 상황에서는 기존 suspended 정리.
                                if (incomingMessage.UserSuid != 0)
                                {
                                    await Server.RemoveRemoteByUserSuid(incomingMessage.UserSuid);
                                }

                                await Server.AllocateNewRemoteAsync(this);
                            }

                            //내부에서 수신된 메시지를 Owner의 수신큐로 이동해주자.
                            //Owner가 잡힌 상태에서는 Owner의 큐를 사용해야함.

                            //Attach된 이후에는 Owner의 메시지큐를 사용해야함.

                            //todo 이름은 변경해주자.
                            Owner?.OnAuthenticated();

                            return;
                        }
                    }
                    else
                    {
                        if (incomingMessage.MessageType == MessageType.HandshakeRes && !IsEstablished)
                        {
                            IsEstablished = true;

                            //todo 뭔가 처리가 누락되는건 아닌지 확인하자.
                            Owner.SessionId = incomingMessage.SessionId;
                            Owner.OnAuthenticated();
                        }
                    }

                    if (!IsEstablished && incomingMessage.MessageType == MessageType.User)
                    {
                        //todo 사용자 메시지 타입을 프린트하자.
                        _logger.Warn("A user message was received before authentication.");

                        incomingMessage.Return();
                        incomingMessage = null;
                        continue;
                    }


                    //todo 아래 메시지는 Attached가 된 상태가 아니라면 처리할 수 없다.
                    //명확하게 구분지어서 처리하도록 하자.

                    //todo 몇개를 잃어버리는건가?

                    // Seq?
                    if (incomingMessage.Seq.HasValue)
                    {
                        if (!Owner.OnSeqReceived(incomingMessage.Seq.Value))
                        {
                            needToDisconnect = true;
                            return;
                        }
                    }

                    // Ack?
                    if (incomingMessage.Ack.HasValue)
                    {
                        Owner.OnAckReceived(incomingMessage.Ack.Value);
                    }

                    // Body?
                    if (incomingMessage.Body.Length > 0)
                    {
                        if (await OnProcessAsync(incomingMessage.Body) == false)
                        {
                            needToDisconnect = true;
                            return;
                        }
                    }

                    incomingMessage.Return();
                    incomingMessage = null;
                }
            }
            catch (InvalidProtocolException)
            {
                _logger.Debug("Invalid Protocol");

                needToDisconnect = true;
            }
            catch (Exception e)
            {
                _logger.Error(e);

                needToDisconnect = true;
            }
            finally
            {
                incomingMessage?.Return();

                if (needToDisconnect)
                {
                    // 오류던 뭐던 의도적으로 끊은 상황.
                    await DisconnectAsync(disconnectReason);
                }
            }
        }

        // ClosingTicket 시스템을 구현하면 문제가 사라질듯 싶다.
        internal async Task<bool> DisconnectAsync(DisconnectReason disconnectReason = DisconnectReason.ByLocal, bool suppressOwnerCallback = false)
        {
            _logger.Debug($"DisconnectAsync: DisconnectReason={disconnectReason}");

            bool shouldHardClose =  disconnectReason == DisconnectReason.ByLocal ||
                                    disconnectReason == DisconnectReason.Replace ||
                                    disconnectReason == DisconnectReason.GlobalSessionExpired;

            if (!DisableReconnecting)
            {
                if (!shouldHardClose)
                {
                    if (Server != null)
                    {
                        await Server.AddToSuspendedAsync((TcpRemote)Owner);
                        suppressOwnerCallback = true; // 이상황에서는 무조건 콜백안함.
                    }
                }
                else
                {
                    lock (_messagesLock)
                    {
                        foreach (var m in _messages)
                        {
                            m.Return();
                        }

                        _messages.Clear();
                    }
                }
            }
            else
            {
                suppressOwnerCallback = false;
            }

            var socket = _socket;
            if (socket != null && Interlocked.CompareExchange(ref _socket, null, socket) == socket)
            {
                try { _cts?.Cancel(); } catch { }
                try { socket?.Shutdown(SocketShutdown.Both); } catch { }
                try { socket?.Close(); } catch { }
            }

            if (shouldHardClose)
            {
                lock (_sendingLock)
                {
                    while (_first.Count > 0)
                    {
                        _first.Dequeue().Return();
                    }

                    while (_pending.Count > 0)
                    {
                        _pending.Dequeue().Return();
                    }

                    while (_sending.Count > 0)
                    {
                        _sending.Dequeue().Return();
                    }

                    while (_unsent.Count > 0)
                    {
                        _unsent.Dequeue().Return();
                    }
                }
            }

            if (!suppressOwnerCallback)
            {
                var wasDisconnectCalled = _wasDisconnectCalled;

                if (wasDisconnectCalled != 0 ||
                    Interlocked.CompareExchange(ref _wasDisconnectCalled, 1, wasDisconnectCalled) != wasDisconnectCalled)
                {
                    return false;
                }

                try { await OnDisconnectAsync(disconnectReason); }
                catch (Exception e) { _logger.Error(e); }
            }

            return true;
        }
    }
}
