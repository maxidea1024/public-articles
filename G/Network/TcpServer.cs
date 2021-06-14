using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using G.Network.Messaging;
using G.Util;
using HaeginDatabase;
using PlayTogether;
using PlayTogetherSocket;

namespace G.Network
{
    public abstract class TcpServer
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private const int HouseKeepingInterval = 1_000;

        //todo 세션 오버랩되는 이슈가 있을수 있어서, 10초 정도로 조정
        //근본적으로는 userUID로 기존 suspended remote객체를 제거하는게 좋을듯.
        //만약 중복 커넥션이라면?
        private const int SuspendedRemoteTimeout = 20_000; //todo 외부 configuration에서 가져오도록 하자.
        private const int CandidateTimeout = 8_000;
        private const int LongIdleTimeout = 1000 * 60 * 2;

        public IPAddress Ip => IPAddress.Any;
        public int Port { get; private set; }
        public Type RemoteType { get; private set; }
        public int RemoteMax { get; private set; }

        private InterlockedFlag _running = new InterlockedFlag();
        private Socket _listener;
        private CancellationTokenSource _cts;
        private SemaphoreSlim _acceptSemaphore;

        protected SemaphoreLock _semaphoreLock = new SemaphoreLock(TimeSpan.FromSeconds(30));
        //protected readonly AsyncLock _semaphoreLock = new AsyncLock();

        private Queue<TcpRemote> _disconnectedRemotes = new Queue<TcpRemote>();
        private Dictionary<long, TcpRemote> _connectedRemotes = new Dictionary<long, TcpRemote>();

        private Dictionary<long, TcpRemote> _suspendedRemotes = new Dictionary<long, TcpRemote>();
        private List<TcpTransport> _candidates = new List<TcpTransport>();

        public int DisconnectedRemoteCount => _disconnectedRemotes.Count;
        public int ConnectedRemoteCount => _connectedRemotes.Count;
        public int AcceptableCount => _acceptSemaphore.CurrentCount;

        internal UID64Generator _sessionIdGenerator = new UID64Generator(0, 0, 0);


        // Stats
        internal long _firstConnects = 0;
        internal long _tryReconnects = 0;
        internal long _reconnectFails = 0;


        //todo remove gc
        public async Task<TcpRemote[]> GetAllRemotesAsync()
        {
            using (await _semaphoreLock.LockAsync())
            {
                return _connectedRemotes.Values.ToArray();
            }
        }

        public TcpServer(Type remoteType, int remoteMax, int remoteInitialCount = 1000)
        {
            if (remoteInitialCount > remoteMax)
            {
                remoteInitialCount = remoteMax;
            }

            RemoteType = remoteType;
            RemoteMax = remoteMax;

            if (TblServerVariable.NetworkPooling)
            {
                for (int i = 0; i < remoteInitialCount; i++)
                {
                    var remote = CreateRemote();
                    _disconnectedRemotes.Enqueue(remote);
                }
            }
        }

        ~TcpServer()
        {
            StopAsync().Wait();
        }

        private TcpRemote CreateRemote()
        {
            TcpRemote remote = (TcpRemote)Activator.CreateInstance(RemoteType);
            remote.Server = this;
            return remote;
        }

        public bool Start(int port)
        {
            try
            {
                if (!_running.Set())
                {
                    return false;
                }

                Port = port;

                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.NoDelay = true;
                _listener.LingerState = new LingerOption(true, 0);

                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                _listener.Bind(endPoint);
                _listener.Listen(100);

                OnStart(_listener);

                _cts = new CancellationTokenSource();

                _acceptSemaphore = new SemaphoreSlim(RemoteMax, RemoteMax);

                // Start accept loop.
                _ = Task.Run(async () => await RunToAcceptAsync(), _cts.Token);

                // Start house-keeping loop.
                _ = Task.Run(async () => await HouseKeepingAsync(), _cts.Token);

                return true;
            }
            catch (Exception e)
            {
                _logger.Error(e);
                return false;
            }
        }

        private async Task RunToAcceptAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                var willLog = false;

                try
                {
                    await _acceptSemaphore.WaitAsync(_cts.Token);

                    var acceptedSocket = await _listener.AcceptAsync();

                    _logger.Debug($"Accept: RemoteAddress={acceptedSocket.RemoteEndPoint}");

                    willLog = true;
                    await CheckOutAsync(acceptedSocket);
                }
                catch (Exception e)
                {
                    if (willLog)
                    {
                        _logger.Error(e);
                    }

                    //todo 만약 어떤 이유로 실패하면, 소켓을 닫아주어야.

                    _acceptSemaphore.Release();
                }
            }
        }

        public void Stop(TimeSpan? timeout = null)
        {
            StopAsync(timeout).Wait();
        }

        public async Task StopAsync(TimeSpan? timeout = null)
        {
            if (!_running.Reset())
            {
                return;
            }

            timeout ??= TimeSpan.FromSeconds(10);

            _cts?.Cancel();

            _listener.Dispose();

            OnStop();

            //Lock 이슈가 있음.

            //using (await _semaphoreLock.LockAsync())
            {
                foreach (var candidate in _candidates)
                {
                    try { await candidate.DisconnectAsync(DisconnectReason.ByLocal); } catch { }
                }
                _candidates.Clear();
            }

            //using (await _semaphoreLock.LockAsync())
            {
                foreach (var suspended in _suspendedRemotes)
                {
                    try { await suspended.Value.DisconnectAsync(DisconnectReason.ByLocal); } catch { }
                }
                _suspendedRemotes.Clear();
            }

            var remotes = await GetAllRemotesAsync();
            foreach (var remote in remotes)
            {
                try { await remote.DisconnectAsync(DisconnectReason.ByLocal); } catch { }
            }

            DateTime outTime = DateTime.Now + timeout.Value;
            while (true)
            {
                if (ConnectedRemoteCount <= 0) break;
                if (DateTime.Now > outTime) break;

                await Task.Delay(1000);
            }
        }

        private async Task HouseKeepingAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(HouseKeepingInterval);

                long outgoingMessageTotalRentCount = OutgoingMessage.TotalRentCount;
                long outgoingMessageTotalReturnCount = OutgoingMessage.TotalReturnCount;
                long incomingMessageTotalRentCount = IncomingMessage.TotalRentCount;
                long incomingMessageTotalReturnCount = IncomingMessage.TotalReturnCount;

                long outgoingUsedCount = outgoingMessageTotalRentCount - outgoingMessageTotalReturnCount;
                long incomingUsedCount = incomingMessageTotalRentCount - incomingMessageTotalReturnCount;

                using (await _semaphoreLock.LockAsync())
                {
                    //todo 통계 수치 항목을 추가하자.
                    // - 재접속 시도 횟수
                    // - 신규 접속 횟수
                    // - 전체 송신량
                    // - 전체 수신량
                    // - 전체 연결 횟수
                    // - 전체 해제 횟수
                    // - 메시지 객체 풀링 상태

                    string poolingFlag = TblServerVariable.NetworkPooling ? "ON" : "OFF";
                    _logger.Info($"SESSIONS   > Connecteds={_connectedRemotes.Count}, Suspendeds={_suspendedRemotes.Count}, Candidates={_candidates.Count}, Disconnecteds={_disconnectedRemotes.Count}, FirstConnects={_firstConnects}, TryReconnects={_tryReconnects}, ReconnectFails={_reconnectFails} (POOLING:{poolingFlag})");

                    _logger.Info($"MESSAGEPOOL> Outgoing=(Useds={outgoingUsedCount},Rents={outgoingMessageTotalRentCount},Returns={outgoingMessageTotalReturnCount}), Incoming=(Used={incomingUsedCount},Rents={incomingMessageTotalRentCount},Returns={incomingMessageTotalReturnCount})");
                }

                // Purge all expired suspended remotes.
                await PurgeExpiredSuspendedRemotesAsync();

                // Purge all expired candidates.
                await PurgeExpiredCandidatesAsync();

                // Purge all long idle remotes.
                await PurgeLongIdleRemotesAsync();

                //DebugList.Add(rented);
                lock (OutgoingMessage.DebugListLock)
                {
                    if (OutgoingMessage.DebugList.Count > 0)
                    {
                        _logger.Debug($"PooledMessages: {OutgoingMessage.DebugList.Count}");

                        for (int i = 0; i < OutgoingMessage.DebugList.Count; i++)
                        {
                            var m = OutgoingMessage.DebugList[i];

                            _logger.Debug($"  #{i+1} => {m.MessageType}:{m.Body.GetType().ToString()}");
                        }
                    }
                }
            }
        }

        private async Task PurgeExpiredSuspendedRemotesAsync()
        {
            var now = SystemClock.Milliseconds;

            List<TcpRemote> expireds = null;
            using (await _semaphoreLock.LockAsync())
            {
                foreach (var pair in _suspendedRemotes)
                {
                    var remote = pair.Value;

                    //todo 보관하고 있는 SentMessages가 과할때도 접속을 종료하는게 좋을듯..
                    if ((now - pair.Value.SuspendedTime) > SuspendedRemoteTimeout)
                    {
                        _suspendedRemotes.Remove(remote.SessionId);

                        _logger.Debug($"Purge expired suspended remote: SessionId={remote.SessionId}, RemoteAddr={remote.RemoteEndPoint}, SuspendedTime={remote.SuspendedTime}");

                        remote.DisableReconnecting();
                        _ = remote.DisconnectAsync(DisconnectReason.ByLocal);
                    }
                }
            }
        }

        private async Task PurgeExpiredCandidatesAsync()
        {
            var now = SystemClock.Milliseconds;

            using (await _semaphoreLock.LockAsync())
            {
                for (int i = _candidates.Count-1; i >= 0; i--)
                {
                    var candidate = _candidates[i];

                    if ((now - candidate.CreatedTime) > CandidateTimeout)
                    {
                        _candidates.RemoveAt(i);

                        //todo 현재 상태도 같이 출력해주면 좋을듯..어디까지 진행되었는지 판단할 수 있을테니..
                        _logger.Debug($"Purge expired candidate: RemoteAddr={candidate.RemoteEndPoint}, CreatedTime={candidate.CreatedTime}");

                        //remote.DisableReconnecting();
                        candidate.DisableReconnecting = true;
                        _ = candidate.DisconnectAsync(DisconnectReason.ByLocal);
                    }
                }
            }
        }

        private async Task PurgeLongIdleRemotesAsync()
        {
            int connectedRemoteCount = 0;

            var now = SystemClock.Milliseconds;

            using (await _semaphoreLock.LockAsync())
            {
                connectedRemoteCount = _connectedRemotes.Count;

                foreach (var pair in _connectedRemotes)
                {
                    var remote = pair.Value;

                    if ((now - remote.LastRecvTime) > LongIdleTimeout)
                    {
                        _logger.Debug($"Purge long idle remote: SessionId={remote.SessionId}, RemoteAddr={remote.RemoteEndPoint}");

                        remote.DisableReconnecting();
                        _ = remote.DisconnectAsync(DisconnectReason.ByLocal);
                    }
                }
            }
        }

        internal async Task CheckOutAsync(Socket acceptedSocket)
        {
            TcpTransport transport = null;

            try
            {
                //todo Pooling..
                //_logger.Debug($"CheckOutAsync: RemoteAddress={acceptedSocket.RemoteEndPoint}");

                transport = new TcpTransport();
                transport.CreatedTime = SystemClock.Milliseconds;
                transport.Server = this;
                await transport.InitializeAsync(acceptedSocket);

                using (await _semaphoreLock.LockAsync())
                {
                    // Since the session is not established yet, it is added to the candidate list.
                    // If a session is not established for a certain period of time, the socket is closed and raised from the list.
                    // The processing is handled by HouseKeeping.
                    if (!_candidates.Contains(transport))
                    {
                        _candidates.Add(transport);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);

                await transport.DisconnectAsync();

                _acceptSemaphore.Release();

                throw;
            }
        }

        internal async Task<bool> CheckInAsync(long sessionId, DisconnectReason disconnectReason)
        {
            using (await _semaphoreLock.LockAsync())
            {
                _logger.Debug($"TcpServer.CheckInAsync: SessionId={sessionId}, DisconnectReason={disconnectReason}");

                if (_connectedRemotes.TryGetValue(sessionId, out var remote))
                {
                    _logger.Debug($"CheckInAsync: SessionId={sessionId}, RemoteAddr={remote.RemoteEndPoint}");

                    remote.ResetSessionId();

                    _connectedRemotes.Remove(sessionId);

                    if (TblServerVariable.NetworkPooling)
                    {
                        _disconnectedRemotes.Enqueue(remote);
                    }

                    _acceptSemaphore.Release();
                    return true;
                }

                return false;
            }
        }

        public async Task<TcpRemote> FindAsync(long sessionId, bool needToLock = true)
        {
            using (await _semaphoreLock.LockAsync(needToLock))
            {
                if (_connectedRemotes.TryGetValue(sessionId, out var remote))
                {
                    return remote;
                }
                else
                {
                    return null;
                }
            }
        }

        protected virtual void OnStart(Socket socket)
        {
        }

        protected virtual void OnStop()
        {
        }

        protected virtual async Task OnAcceptAsync(TcpRemote remote)
        {
        }

        protected virtual async Task OnCheckInAsync(TcpRemote remote)
        {
        }

        internal async Task RemoveCandidateAsync(TcpTransport candidate)
        {
            using (await _semaphoreLock.LockAsync())
            {
                //_candidates.Remove(candidate);
                _candidates.RemoveAll(x => x == candidate);
            }
        }

        internal async Task AddToSuspendedAsync(TcpRemote remote)
        {
            if (remote == null)
            {
                return;
            }

            _logger.Debug($"AddToSuspendedAsync: SessionId={remote.SessionId}");

            using (await _semaphoreLock.LockAsync())
            {
                if (!_suspendedRemotes.ContainsKey(remote.SessionId))
                {
                    remote.SuspendedTime = SystemClock.Milliseconds;
                    _suspendedRemotes.Add(remote.SessionId, remote);
                }
            }
        }

        internal async Task RemoveFromSuspendedAsync(TcpRemote remote)
        {
            if (remote == null)
            {
                return;
            }

            using (await _semaphoreLock.LockAsync())
            {
                _suspendedRemotes.Remove(remote.SessionId);
            }
        }

        internal async Task<TcpRemote> FindRemoteForRecoverAsync(long sessionId)
        {
            using (await _semaphoreLock.LockAsync())
            {
                TcpRemote remote = null;

                // First, find in _suspendedRemotes.
                if (_suspendedRemotes.TryGetValue(sessionId, out remote))
                {
                    _suspendedRemotes.Remove(sessionId);
                    return remote;
                }

                // Let's find out among the objects that are already connected.
                // (Clear tried to reconnect through a new connection, but the server did not detect the disconnection of the client)
                if (_connectedRemotes.TryGetValue(sessionId, out remote))
                {
                    return remote;
                }
            }

            return null;
        }

        internal async Task<TcpRemote> AllocateNewRemoteAsync(TcpTransport transport)
        {
            var now = SystemClock.Milliseconds;

            TcpRemote remote = null;
            using (await _semaphoreLock.LockAsync())
            {
                if (TblServerVariable.NetworkPooling)
                {
                    if (_disconnectedRemotes.TryPeek(out remote))
                    {
                        if (now >= remote.UsableTime)
                        {
                            _disconnectedRemotes.Dequeue();
                        }
                        else
                        {
                            remote = null;
                        }
                    }
                }

                if (remote == null)
                {
                    remote = CreateRemote();

                    _logger.Debug($"RemotePool: Allocated new remote. InstanceId={remote.InstanceId}");
                }
                else
                {
                    _logger.Debug($"RemotePool: Recycle remote. InstanceId={remote.InstanceId}");
                }

                // Allocate new session id.
                remote.SessionId = (long)_sessionIdGenerator.Next();

                _connectedRemotes.Add(remote.SessionId, remote);
            }

            // Do not change call order.
            _logger.Debug($"AllocateNewRemoteAsync: SessionId={remote.SessionId}");

            await OnAcceptAsync(remote);
            await remote.AssignNewTransportAsync(transport);

            return remote;
        }

        internal async Task<bool> RemoveRemoteByUserSuid(long userSuid)
        {
            List<TcpRemote> list = null;

            using (await _semaphoreLock.LockAsync())
            {
                //todo optimization
                foreach (var pair in _suspendedRemotes)
                {
                    if (pair.Value.userUID == userSuid)
                    {
                        list ??= new List<TcpRemote>();
                        list.Add(pair.Value);
                    }
                }
            }

            if (list != null)
            {
                foreach (var deletee in list)
                {
                    _logger.Debug($"....RemoveRemoteByUserSuid: {userSuid}");

                    var kick = new Kicked { Result = Result.ReconnectUserRemoved, };
                    deletee.SendPacket(kick);

                    await Task.Delay(3_000);

                    await deletee.DisconnectAsync(DisconnectReason.ByLocal);
                }

                return true;
            }

            return false;
        }
    }
}
