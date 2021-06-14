using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using G.Util;
using System.Collections.Generic;

namespace G.Network
{
	public abstract class TcpSocketLegacy
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

		public CancellationTokenSource Cancellation { get; protected set; }

		public LinearBufferStream RecvStream { get; private set; } = new LinearBufferStream(BufferSize);
		public LinearBufferStream SendStream { get; private set; } = new LinearBufferStream(BufferSize);

		public Socket Socket { get; private set; }

		protected KeyChain keyChain = new KeyChain();
		public KeyChain KeyChain { get { return keyChain; } }

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

		public TcpSocketLegacy()
	    {
			AutoProcess = true;

			__close = 0;
			__sends = 0;
			__recvs = 0;
		}

		public TcpSocketLegacy(string host, int port) : this()
		{
			SetHostPort(host, port);
		}

		public TcpSocketLegacy(IPAddress host, int port) : this()
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
				InitializeSendStreamPool();
				packetSizeList.Clear();
				//----------------------------

				Cancellation = new CancellationTokenSource();

				Socket = socket;
				socket.NoDelay = true;
				socket.LingerState = new LingerOption(true, 0);

				keyChain.Reset();
				keyChain.Set(KeyIndex.Remote);

				RecvStream.Flush();
				SendStream.Flush();

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


		public virtual void Send(byte[] buffer, int offset, int count, KeyIndex keyIndex, PlayTogetherSocket.ProtocolId protocolID, bool alreadyEncrypted = false)
		{
			Send(buffer.AsMemory(offset, count), keyIndex, protocolID, alreadyEncrypted);
			//Send2(buffer.AsMemory(offset, count), keyIndex, alreadyEncrypted);
		}

		public virtual void Send(ReadOnlyMemory<byte> memory, KeyIndex keyIndex, PlayTogetherSocket.ProtocolId protocolID, bool alreadyEncrypted = false)
		{
			var socket = Socket;

			var socketId = Id;
			if (socketId == 0) return;

			(byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);

			try
			{
				if (alreadyEncrypted) { }
				else if (keyIndex != KeyIndex.None)
				{
					bm = KeyChain.EncryptUsingArrayPool(keyIndex, memory);
					if (bm.Buffer != null)
					{
						memory = bm.Memory;
					}
				}

				int count = memory.Length + 3;
				var stream = SendStream;

				lock (sendLock)
				{
					//--------------
					packetSizeList.Add((protocolID, count, DateTime.UtcNow));
					if (10 < packetSizeList.Count)
						packetSizeList.RemoveAt(0);
					//--------------

					var wm = stream.WritableMemory;
					if (wm.Length < count)
					{
						//isBufferFull = true;

						log.Error($"SendBuffer Full. userUID: {this.userUID}, Readable: {stream.Readable}, Writable: {stream.Writable}, Capacity: {stream.Capacity}, OffsetR: {stream.OffsetR}, OffsetW: {stream.OffsetW}, isSending: {isSending}, lastSendTime: {lastSendTime.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}");
						var packetSizeStr = "packetPair. (protocolID, size, dateTime). ";
						foreach(var pair in packetSizeList)
                        {
							packetSizeStr += string.Format($"({pair.Item1}, {pair.Item2}, {pair.Item3}), ");
						}
						log.Error($"SendBuffer Full. userUID: {this.userUID}, {packetSizeStr}");
						var str = string.Format("SendBuffer Full: {0} [ {1}, {2}, {3} ], network[ {4}, {5}, {6}, {7} ]",
							this.userUID, this.Id, count, wm.Length, alreadyEncrypted, keyIndex, this.__sends, this.__recvs);
						log.Error($"{str}");
						//throw new Exception(str);
						stream.EnsureForWritable(count);
						wm = stream.WritableMemory;
					}

					var needToSend = (stream.Readable <= 0);

					byte[] countBytes = BitConverter.GetBytes((ushort)count);
					var keyBytes = new byte[] { (byte)keyIndex };

					countBytes.CopyTo(wm);
					keyBytes.CopyTo(wm.Slice(2));
					memory.CopyTo(wm.Slice(3));
					stream.OnWrite(count);

					__sends1++;
					if (needToSend == true) isSending = true;

					if (needToSend == false) return;
				}

				Task.Run(async () => await RunToSendAsync(socket, socketId));
			}
			catch (Exception ex)
			{
				log.Debug(ex);
				Task.Run(async () => await DisconnectAsync(socketId));
			}
			finally
			{
				if (bm.Buffer != null) ArrayPool<byte>.Shared.Return(bm.Buffer);
			}
		}

		protected async Task RunToSendAsync(Socket socket, long socketId)
		{
			var stream = SendStream;

			try
			{
				while (socketId != 0 && socketId == Id && !Cancellation.IsCancellationRequested)
				{
					//-----------
					lastSendTime = DateTime.UtcNow;
					//-----------
					var sentBytes = await socket.SendAsync(stream.ReadableMemory, SocketFlags.None, Cancellation.Token);
					if (sentBytes <= 0) throw new SocketException();

					__sendc1++;
					var timeSpan = DateTime.UtcNow - lastSendTime;
					if (3000 <= timeSpan.TotalMilliseconds)
                    {
						log.Error($"RunToSendAsync. SendAsync delay 3000 ms over. userUID: {this.userUID}, delayMs: {timeSpan.TotalMilliseconds}");
                    }

					lock (sendLock)
					{
						stream.OnRead(sentBytes);
						if (stream.Readable <= 0)
						{
							stream.Optimize();
							isSending = false;
							break;
						}
					}
				}
			}
			catch (SocketException ex)
			{
				//if (SocketError.Success != ex.SocketErrorCode)
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
			//bool willLog = false;

			try
			{
				while (socketId == Id && !Cancellation.IsCancellationRequested)
				{
					//willLog = false;
					int readBytes = await socket.ReceiveAsync(stream.WritableMemory, SocketFlags.None, Cancellation.Token);
					if (readBytes <= 0) throw new SocketException();

					__recvc1++;

					//willLog = true;
					stream.OnWrite(readBytes);
					if (AutoProcess) await ProcessAsync(socketId);

					if (socketId != Id)
						log.Error($"RunToReceiveAsync. socketId != Id. userUID: {this.userUID}, id: {this.Id}, socketId: {socketId}");

					if (true == Cancellation.IsCancellationRequested)
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

		// If AutoProcess is true, This function is called automatically when data is received.
		// If AutoProcess is false, You meed to called this function manually.
		public async Task ProcessAsync(long socketId)
		{
			var needToDisconnect = false;
			var stream = RecvStream;

			(byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);

			try
			{
				while (socketId == Id && !Cancellation.IsCancellationRequested)
				{
					var memory = stream.ReadableMemory;

					if (memory.Length < 3) return;

					int length = (int)BitConverter.ToUInt16(memory.Span);
					if (length < 3)
					{
						needToDisconnect = true;
						return;
					}
					if (length > memory.Length) return;

					int keyIndex = memory.Span[2];
					var protocolLength = length - 3;
					var protocolMemory = memory.Slice(3, protocolLength);

					bm = KeyChain.DecryptUsingArrayPool((KeyIndex)keyIndex, protocolMemory);
					if (bm.Buffer != null)
					{
						if (await OnProcessAsync(bm.Memory) == false)
						{
							needToDisconnect = true;
							return;
						}
					}
					else if (keyIndex > 0)
					{
						log.Debug("------------------------------------------------ Invalid Key");
					}
					else
					{
						if (await OnProcessAsync(protocolMemory) == false)
						{
							needToDisconnect = true;
							return;
						}
					}

					__recvs1++;

					stream.OnRead(length);
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
				if (bm.Buffer != null) ArrayPool<byte>.Shared.Return(bm.Buffer);

				if (needToDisconnect)
				{
					await DisconnectAsync(socketId);
				}
				else
				{
					stream.Optimize();
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


		//----------------------------
		public static int SendStreamCount { get; private set; } = 10;
		public List<LinearBufferStream> AllSendStreamPool { get; private set; } = new List<LinearBufferStream>();		 // 모든 SendStream 에 대한 Pool
		public Queue<LinearBufferStream> UsableSendStreamPool { get; private set; } = new Queue<LinearBufferStream>();   // 사용 가능한 SendStream 에 대한 List
		public List<LinearBufferStream> SendWaitStreamList { get; private set; } = new List<LinearBufferStream>();		 // 사용중인 SendStream 에 대한 List
		public LinearBufferStream CurrentSendStream { get; private set; } = null;      // 사용중인 SendStream
		public bool IsRunToSend { get; private set; } = false;

		private void InitializeSendStreamPool()
        {
			return;
			/*
			if (0 == AllSendStreamPool.Count)
            {
				for (int i = 0; i < SendStreamCount; ++i)
                {
					AllSendStreamPool.Add(new LinearBufferStream(BufferSize));
				}
            }

			UsableSendStreamPool.Clear();
			SendWaitStreamList.Clear();
			CurrentSendStream = null;

			foreach(var stream in AllSendStreamPool)
            {
				stream.Reset();
				UsableSendStreamPool.Enqueue(stream);
			}
			*/
		}

		private LinearBufferStream AllocSendStream()
        {
			if (0 == UsableSendStreamPool.Count)
				return null;

			var stream = UsableSendStreamPool.Dequeue();
			AddSendWaitStream(stream);
			return stream;
        }

		private void FreeSendStream(LinearBufferStream stream)
		{
			stream.Reset();
			UsableSendStreamPool.Enqueue(stream);
		}

		private LinearBufferStream PopNextSendWaitStream()
        {
			if (0 == SendWaitStreamList.Count)
				return null;

			var stream = SendWaitStreamList[0];
			SendWaitStreamList.RemoveAt(0);
			return stream;
        }

		private LinearBufferStream GetLastSendWaitStream()
		{
			if (0 == SendWaitStreamList.Count)
				return null;

			return SendWaitStreamList[SendWaitStreamList.Count - 1];
		}

		private void AddSendWaitStream(LinearBufferStream stream)
        {
			SendWaitStreamList.Add(stream);
		}

		public virtual void Send2(ReadOnlyMemory<byte> memory, KeyIndex keyIndex, bool alreadyEncrypted = false)
		{
			var socket = Socket;

			var socketId = Id;
			if (socketId == 0) return;

			(byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);

			try
			{
				if (alreadyEncrypted) { }
				else if (keyIndex != KeyIndex.None)
				{
					bm = KeyChain.EncryptUsingArrayPool(keyIndex, memory);
					if (bm.Buffer != null)
					{
						memory = bm.Memory;
					}
				}

				int count = memory.Length + 3;
				//var stream = SendStream;

				if (BufferSize < count)
                {
					// 한번에 보내려는 패킷 사이즈가 버퍼 사이즈보다 크다
					isBufferFull = true;
					throw new Exception($"packet size over. userUID: {this.userUID}, BufferSize: {BufferSize}, packetSize: {count}");
				}

				lock (sendLock)
				{
					var stream = CurrentSendStream;
					if (null == stream || stream.WritableMemory.Length < count)
                    {
						stream = GetLastSendWaitStream();
						if (null == stream || stream.WritableMemory.Length < count)
							stream = AllocSendStream();
					}

					if (null == stream)
                    {
						// 사용 사능한 stream 이 없는 경우
						isBufferFull = true;
						var str = string.Format($"sendStream null. userUID: {this.userUID}, AllSendStreamPool: {AllSendStreamPool.Count}, UsableSendStreamPool: {UsableSendStreamPool.Count}, SendWaitStreamList: {SendWaitStreamList.Count}");
						throw new Exception(str);
					}

					var wm = stream.WritableMemory;
					if (wm.Length < count)
                    {
						// stream 이 제대로 초기화 안된 경우
						isBufferFull = true;
						log.Error($"SendBuffer Full. userUID: {this.userUID}, Readable: {stream.Readable}, Writable: {stream.Writable}, Capacity: {stream.Capacity}, OffsetR: {stream.OffsetR}, OffsetW: {stream.OffsetW}, isSending: {isSending}, lastSendTime: {lastSendTime.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}");
						var str = string.Format("SendBuffer Full: {0} [ {1}, {2}, {3} ], network[ {4}, {5}, {6}, {7} ]",
							this.userUID, this.Id, count, wm.Length, alreadyEncrypted, keyIndex, this.__sends, this.__recvs);
						throw new Exception(str);
					}

					byte[] countBytes = BitConverter.GetBytes((ushort)count);
					var keyBytes = new byte[] { (byte)keyIndex };

					countBytes.CopyTo(wm);
					keyBytes.CopyTo(wm.Slice(2));
					memory.CopyTo(wm.Slice(3));
					stream.OnWrite(count);

					__sends1++;
					if (IsRunToSend == true) return;

					IsRunToSend = true;
					CurrentSendStream = PopNextSendWaitStream();
				}

				Task.Run(async () => await RunToSend2Async(socket, socketId));
			}
			catch (Exception ex)
			{
				log.Debug(ex);
				Task.Run(async () => await DisconnectAsync(socketId));
			}
			finally
			{
				if (bm.Buffer != null) ArrayPool<byte>.Shared.Return(bm.Buffer);
			}
		}

		protected async Task RunToSend2Async(Socket socket, long socketId)
		{
			//var stream = SendStream;

			try
			{
				while (socketId != 0 && socketId == Id && !Cancellation.IsCancellationRequested)
				{
					//-----------
					lastSendTime = DateTime.UtcNow;
					//-----------
					var stream = CurrentSendStream;
					var sentBytes = await socket.SendAsync(stream.ReadableMemory, SocketFlags.None, Cancellation.Token);
					if (sentBytes <= 0) throw new SocketException();

					__sendc1++;

					lock (sendLock)
					{
						stream.OnRead(sentBytes);
						if (stream.Readable <= 0)
						{
							stream.Reset();
							FreeSendStream(stream);

							CurrentSendStream = PopNextSendWaitStream();
							if (null == CurrentSendStream)
							{
								IsRunToSend = false;
								isSending = false;
								break;
							}
						}
					}
				}
			}
			catch (SocketException ex)
			{
				//if (SocketError.Success != ex.SocketErrorCode)
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
		//----------------------------
	}
}
