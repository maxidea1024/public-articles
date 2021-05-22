using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using G.Util;
using System.Collections.Generic;
using G.Network.Messaging;
using MessagePack;
using PlayTogether;
using PlayTogetherSocket;
using Renci.SshNet.Messages;

namespace G.Network
{
	public abstract class TcpSocket
	{
		private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		public static int BufferSize { get; private set; } = 32768;

		private static long id = 0;
		public long Id;

		//-----------------------------------
		public long userUID;
		public bool isBufferFull = false;
		public bool isSending = false;
		public DateTime lastSendTime = DateTime.MinValue;
		List<(PlayTogetherSocket.ProtocolId, int, DateTime)> packetSizeList = new List<(PlayTogetherSocket.ProtocolId, int, DateTime)>();
		//-----------------------------------

        // These can be accessed by TcpServer
		internal TcpServer Server;

		public CancellationTokenSource Cancellation { get; protected set; }

		//public LinearBufferStream RecvStream { get; private set; } = new LinearBufferStream(BufferSize);
		//public LinearBufferStream SendStream { get; private set; } = new LinearBufferStream(BufferSize);
        public ByteStreamQueue RecvStream { get; private set; } = new ByteStreamQueue(BufferSize);
        public ByteStreamQueue SendStream { get; private set; } = new ByteStreamQueue(BufferSize);

		public Socket Socket { get; private set; }

		protected KeyChain keyChain = new KeyChain();
		public KeyChain KeyChain => keyChain;

		public IPAddress Host { get; protected set; }
		public int Port { get; protected set; }
		public IPEndPoint EndPoint { get; protected set; }

		protected SemaphoreSlim semaphoreConn = new SemaphoreSlim(1, 1);
		protected object sendLock = new object();

		public bool AutoProcess { get; set; }

		public bool IsConnected
		{
			get
			{
				try { return Socket.Connected; }
				catch (Exception) { return false; }
			}
		}


        #region Reliable Session
        private uint? LastRecvSeq;
        private uint? LastSentAck;
        private uint NextMessageSeq;
        private ulong SessionId = 0;

        private List<BaseProtocol> UnsentMessagesRaw = new List<BaseProtocol>(); // 기존거 호환목적
        private List<OutgoingMessage> UnsentMessages = new List<OutgoingMessage>();
        private List<OutgoingMessage> FirstSendMessages = new List<OutgoingMessage>();
        private Queue<OutgoingMessage> SentMessages = new Queue<OutgoingMessage>();
        private float DelayedSendAckInterval = 2.5f;

        public bool UseReliableSession = false;

        public DateTime SuspendedTime { get; set; }
        protected TaskTimer DelayedSendAckTimer;
        #endregion


        #region Reliable Session

        //todo 어디선가 호출해줘야하는데...
        //DelayedSendAckTimer = new TaskTimer(() => OnDelayedSendAck(), DelayedSendAckInterval, DelayedSendAckInterval);

        private void OnAckReceived(uint ack)
        {
            while (SentMessages.Count > 0)
            {
                var message = SentMessages.Peek();

                if (SeqNumberHelper32.Less(message.Seq.Value, ack))
                {
                    SentMessages.Dequeue();

                    // Return to pool.
                    message.Return();
                }
                else
                {
                    break;
                }
            }
        }

        private bool OnSeqReceived(uint seq)
        {
            if (LastRecvSeq.HasValue)
            {
                if (!SeqNumberHelper32.Less(LastRecvSeq.Value, seq))
                {
                    // Log.Warning($"Last sequence number is {LastRecvSeq.Value} but {seq} received. Skipping messages.");
                    return false;
                }

                if (seq != LastRecvSeq.Value + 1)
                {
	                //todo
                    //Disconnect();
                    return false;
                }
            }

            LastRecvSeq = seq;

            if (DelayedSendAckInterval <= 0f)
            {
                SendAck(LastRecvSeq.Value + 1);
            }

            return true;
        }

        private void SendAck(uint ack, bool firstSending = false)
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

            if (LastSentAck == null)
            {
                var message = OutgoingMessage.Rent();
                message.MessageType = MessageType.None;
                message.Ack = ack;
                Send(message);

                LastSentAck = ack;
            }
        }

        // 타이머에서 일정 간격마다 호출되어야함.
        void OnDelayedSendAck()
        {
            // 아직 메시지를 한번도 받지 않았다면, ack를 보낼수가 없음.
            if (LastRecvSeq == null)
            {
                return;
            }

            // 최근에 보낸 ack가 없거나, 받은 seq보다 작을 경우에 ack 전송
            if (LastSentAck == null || SeqNumberHelper32.Less(LastSentAck.Value, LastRecvSeq.Value + 1))
            {
                SendAck(LastRecvSeq.Value + 1);
            }
        }

        //todo
        /*
        void UpdateDelayedSendAckTimer()
        {
            if (DelayedSendAckInterval <= 0f)
            {
                return;
            }

            DelayedSendAckTimer += Time.unscaledTime;
            if (DelayedSendAckTimer >= DelayedSendAckInterval)
            {
                DelayedSendAckTimer -= DelayedSendAckInterval;

                OnDelayedSendAck();
            }
        }
        */

        public void ResetReliableSession()
        {
            var rnd = new System.Random();

            SessionId = 0;
            LastRecvSeq = null;
            LastSentAck = null;
            NextMessageSeq = (uint)rnd.Next() + (uint)rnd.Next();

            //SentMessages.Clear();
            //UnsentMessages.Clear();
            //FirstSendMessages.Clear();

            foreach (var message in SentMessages)
            {
                message.Return();
            }
            SentMessages.Clear();

            foreach (var message in UnsentMessages)
            {
                message.Return();
            }
            SentMessages.Clear();

            foreach (var message in FirstSendMessages)
            {
                message.Return();
            }
            SentMessages.Clear();
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
                var protocolId = message.Body.RawProtocolId;
                switch ((ProtocolId)protocolId)
                {
                    case ProtocolId.AliveQ:
                    case ProtocolId.AliveA:
                    //case ProtocolId.SyncUserQ:
                    //case ProtocolId.ActionUserQ:
                    //case ProtocolId.SyncJumpQ:
                    //case ProtocolId.ChatToAllQ:
                        needSeq = false;
                        break;
                }

                message.KeyIndex = ResolveKeyIndex(message.Body);
            }

            if (needSeq)
            {
                message.Seq = NextMessageSeq++;

                // 메시지를 보낼때 ack를 실어서 보낼 조건이 된다면 첨부하도록 함.
                if (LastRecvSeq.HasValue &&
                    (!LastSentAck.HasValue || SeqNumberHelper32.Less(LastSentAck.Value, LastRecvSeq.Value + 1)))
                {
                    uint ack = LastRecvSeq.Value + 1;
                    message.Ack = ack;
                    LastSentAck = ack;  // 메시지에 실어서 갈것이므로 보낸것으로 간주함.
                }

                SentMessages.Enqueue(message);
            }

            message.Build(SerializeProtocol, KeyChain);
        }

        private void ReencodeMessage(OutgoingMessage message)
        {
            message.Rebuild(KeyChain);
        }

        public void Send(OutgoingMessage message)
        {
            if (message.IsEncoded)
                ReencodeMessage(message);
            else
                EncodeMessage(message);

            SendToWire(message);

            if (!message.Seq.HasValue)
                message.Return();
        }

        public void SendPacket(BaseProtocol protocol)
        {
            //todo 단순 커넥트가 아닌 인증이 안되었으면 보류 시키는 로직으로 처리해야함.
            if (!IsConnected)
            {
                switch ((ProtocolId)protocol.RawProtocolId)
                {
                    case ProtocolId.AliveQ:
                    case ProtocolId.AliveA:
                    //case ProtocolId.SyncUserQ:
                    //case ProtocolId.SyncJumpQ:
                        return;

                    default:
                    {
                        var message = OutgoingMessage.Rent();
                        message.MessageType = MessageType.User;
                        message.Body = protocol;
                        UnsentMessages.Add(message);
                        return;
                    }
                }
            }

            {
                var message = OutgoingMessage.Rent();
                message.MessageType = MessageType.User;
                message.Body = protocol;
                Send(message);
            }
        }

        protected virtual KeyIndex ResolveKeyIndex(BaseProtocol protocol)
        {
	        return KeyIndex.None;
        }

        protected virtual Type GetBaseProtocolType()
        {
            return typeof(object);
        }

        protected virtual void SerializeProtocol(Stream stream, object protocol)
        {
            MessagePackSerializer.Serialize(stream, protocol);
        }

        private void SendInitialAck()
        {
            if (LastRecvSeq.HasValue)
            {
                SendAck(LastRecvSeq.Value + 1);
            }
        }

        //todo 기존 로직일 경우에는 다르게 보내야함.
        private void SendAllUnsentMessages()
        {
            if (UnsentMessages.Count > 0)
            {
                foreach (var message in UnsentMessages)
                {
                    Send(message);
                }

                UnsentMessages.Clear();
            }
        }

        private void SendToWire(OutgoingMessage message)
        {
            if (!IsConnected)
            {
                return;
            }

            SendToWire(message.PackedHeader, message.PackedBody);
        }

        //todo 최종적으로 issue가 완료된 시점에서 message를 return해주면 복사를 줄일 수 있을듯..
		private void SendToWire(ArraySegment<byte> header, ArraySegment<byte> body)
		{
			var socket = Socket;

			var socketId = Id;
			if (socketId == 0) return;

            if (header.Count == 0 && body.Count == 0)
            {
                return;
            }

            try
            {
                lock (sendLock)
                {
                    var shouldBeginSend = (SendStream.Count == 0);

                    SendStream.Enqueue(header);
                    SendStream.Enqueue(body);

                    if (!shouldBeginSend)
                    {
                        return;
                    }
                }

                Task.Run(async () => await RunToSendAsync(socket, socketId));
            }
            catch (Exception e)
            {
				log.Debug(e);
				Task.Run(async () => await DisconnectAsync(socketId));
            }
		}

        private KeyIndex ResolveKeyIndex(ProtocolId protocolId)
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


		public TcpSocket()
	    {
			AutoProcess = true;

			__close = 0;
			__sends = 0;
			__recvs = 0;

            ResetReliableSession();
		}

		public TcpSocket(string host, int port) : this()
		{
			SetHostPort(host, port);
		}

		public TcpSocket(IPAddress host, int port) : this()
		{
			SetHostPort(host, port);
		}

		protected void SetHostPort(string host, int port)
		{
			IPAddress ipAddress;
			if (IPAddress.TryParse(host, out ipAddress))
				SetHostPort(ipAddress, port);
			else
				SetHostPort(Dns.GetHostAddresses(host)[0], port);
		}

		protected void SetHostPort(IPAddress host, int port)
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

		internal virtual async Task InitializeAsync(Socket socket)
		{
			try
			{
				Id = Interlocked.Increment(ref id);

				//----------------------------
				userUID = 0;
				isBufferFull = false;
				isSending = false;
				lastSendTime = DateTime.MinValue;
				packetSizeList.Clear();
				//----------------------------

				Cancellation = new CancellationTokenSource();

				Socket = socket;
				socket.NoDelay = true;
				socket.LingerState = new LingerOption(true, 0);

				keyChain.Reset();
				keyChain.Set(KeyIndex.Remote);

				RecvStream.Clear();
				SendStream.Clear();

				try { await OnConnectAsync(Id); } catch (Exception ex2) { log.Error(ex2); }

				_ = Task.Run(async () => await RunToReceiveAsync(socket, Id));
			}
			catch (Exception)
			{
				try { await OnConnectErrorAsync(-1); } catch (Exception ex2) { log.Error(ex2); }
				throw;
			}
		}

		public virtual async Task<bool> ConnectAsync()
		{
			try
			{
				await DisconnectAsync(Id);

				await semaphoreConn.WaitAsync();

				var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				await socket.ConnectAsync(Host, Port);

				await InitializeAsync(socket);

                var message = OutgoingMessage.Rent();
                message.MessageType = MessageType.HandshakeReq;
                message.SessionId = 0;
                Send(message);

				return true;
			}
			catch (Exception ex)
			{
				// log.Error(ex);
				// if 0 == Id, then no Release..
				// connect 요청 할때 에러가 발생한 경우..

				if (0 == Id && 0 == semaphoreConn.CurrentCount)
				{
					semaphoreConn.Release();
					log.Debug("tried semaphoreConn.Release.. Count[ {0} ]", semaphoreConn.CurrentCount);
				}

				log.Error(ex);

				try { await OnConnectErrorAsync(-1); } catch (Exception ex2) { log.Error(ex2); }
				await DisconnectAsync(Id);

				return false;
			}
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

#pragma warning disable CS1998
		public async Task<bool> DisconnectAsync()
		{
			return await DisconnectAsync(Id);
		}

		public async Task<bool> DisconnectAsync(long socketId)
		{
			if (socketId == 0) return false;

			var oldSocketId = Interlocked.CompareExchange(ref Id, 0, socketId);
			if (oldSocketId != socketId) return false;

			var socket = Socket;

			try { Cancellation?.Cancel(); } catch { }
			try { socket?.Shutdown(SocketShutdown.Both); } catch { }
			try { socket?.Close(); } catch { }

			try { await OnDisconnectAsync(socketId); }
			catch (Exception ex) { log.Error(ex); }

			semaphoreConn.Release();

            ResetReliableSession();

			return true;
		}
#pragma warning restore CS1998

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

		protected async Task RunToSendAsync(Socket socket, long socketId)
		{
			var stream = SendStream;

			try
			{
				while (socketId != 0 && socketId == Id && !Cancellation.IsCancellationRequested)
				{
                    var readableMemory = stream.ReadableMemory;
					var sentBytes = await socket.SendAsync(readableMemory, SocketFlags.None, Cancellation.Token);
					if (sentBytes <= 0)
                    {
                        throw new SocketException();
                    }

					lock (sendLock)
					{
                        stream.DequeueNoCopy(sentBytes);
                        if (stream.IsEmpty)
                        {
                            break;
                        }
					}
				}
			}
			catch (SocketException ex)
			{
				log.Debug($"RunToSendAsync. SocketException. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}, SocketErrorCode: {ex.SocketErrorCode}");
				await DisconnectAsync(socketId);
			}
			catch (OperationCanceledException)
			{
				log.Debug($"RunToSendAsync. OperationCanceledException. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}");
				await DisconnectAsync(socketId);
			}
			catch (Exception ex)
			{
				log.Debug($"RunToSendAsync. Exception. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId},  ex: {ex}");
				await DisconnectAsync(socketId);
			}
		}

		protected async Task RunToReceiveAsync(Socket socket, long socketId)
		{
			var stream = RecvStream;

			try
			{
				while (socketId == Id && !Cancellation.IsCancellationRequested)
				{
                    var writableMemory = stream.WritableMemory;
					int readBytes = await socket.ReceiveAsync(writableMemory, SocketFlags.None, Cancellation.Token);
					if (readBytes <= 0)
                    {
                        throw new SocketException();
                    }

                    stream.EnqueueNoCopy(readBytes);

					if (AutoProcess)
                        await ProcessAsync(socketId);

					if (socketId != Id)
                        log.Error($"RunToReceiveAsync. socketId != Id. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}");

					if (Cancellation.IsCancellationRequested)
                        log.Error($"RunToReceiveAsync. Cancellation.IsCancellationRequested true. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}");
				}
			}
			catch (SocketException ex)
			{
				//if (SocketError.Success != ex.SocketErrorCode)
				log.Debug($"RunToReceiveAsync. SocketException. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}, SocketErrorCode: {ex.SocketErrorCode}");
				await DisconnectAsync(socketId);
			}
			catch (OperationCanceledException)
			{
				log.Debug($"RunToReceiveAsync. OperationCanceledException. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}");
				await DisconnectAsync(socketId);
			}
			catch (Exception ex)
			{
				//if (willLog) log.Error(ex);
				log.Debug($"RunToReceiveAsync. Exception. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId},  ex: {ex}");
				await DisconnectAsync(socketId);
			}
		}

		public async Task ProcessAsync(long socketId)
		{
			var needToDisconnect = false;
			var stream = RecvStream;

            IncomingMessage incomingMessage = null;

			try
			{
				while (socketId == Id && !Cancellation.IsCancellationRequested)
				{
                    if (!IncomingMessage.TryDequeue(stream, KeyChain, out incomingMessage))
                    {
                        break;
                    }

                    if (incomingMessage.MessageType == MessageType.HandshakeReq)
                    {
                        var message = OutgoingMessage.Rent();
                        message.MessageType = MessageType.HandshakeRes;

                        if (incomingMessage.SessionId != 0)
                        {
                            // 재연결 요청

                            // 의도적으로 끊었을때만 Disconnect()에서 호출하도록 하는게 좋을듯..
                            ResetReliableSession();

                            //todo context를 찾아서 재연결 해줘야함!
                            //만약 컨텍스트를 못찾으면 오류 반환.
                        }
                        else
                        {
                            // 처음 연결
                            this.SessionId = Server.SessionIdGenerator.Next();
                        }

                        message.SessionId = this.SessionId;
                        message.RemoteEncryptionKeyIndex = KeyIndex.Remote; //with remote key
                        Send(message);

                        //todo 이건 재연결시에만 의미가 있는거 아닌가?
                        SendInitialAck();

                        SendAllUnsentMessages();

                        return;
                    }

                    // Seq?
                    if (incomingMessage.Seq.HasValue)
                    {
                        if (!OnSeqReceived(incomingMessage.Seq.Value))
                        {
                            needToDisconnect = true;
                            return;
                        }
                    }

                    // Ack?
                    if (incomingMessage.Ack.HasValue)
                    {
                        OnAckReceived(incomingMessage.Ack.Value);
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
				log.Debug("Invalid Protocol");
				needToDisconnect = true;
			}
			catch (Exception ex)
			{
				log.Error(ex);
				needToDisconnect = true;
			}
			finally
			{
				if (incomingMessage != null)
                {
                    incomingMessage.Return();
                    incomingMessage = null;
                }

				if (needToDisconnect)
				{
					await DisconnectAsync(socketId);
				}
			}
		}

#pragma warning disable CS1998
		protected virtual async Task OnConnectAsync(long socketId)
	    {
		}

		protected virtual async Task OnConnectErrorAsync(int error)
		{
		}

		protected virtual async Task OnDisconnectAsync(long socketId)
	    {
		}

		protected virtual async Task<bool> OnProcessAsync(ReadOnlyMemory<byte> memory)
		{
			return true;
	    }
#pragma warning restore CS1998
	}
}
