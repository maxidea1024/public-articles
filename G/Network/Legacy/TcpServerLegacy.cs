using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using G.Util;

namespace G.Network
{
    public abstract class TcpServerLegacy
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        public IPAddress Ip { get { return IPAddress.Any; } }
        public int Port { get; private set; }
        public Type RemoteType { get; private set; }
        public int RemoteMax { get; private set; }

        private InterlockedFlag running = new InterlockedFlag();
        private Socket listener;
        private CancellationTokenSource cancellation;
        private SemaphoreSlim acceptSemaphore;

        protected SemaphoreLock semaphoreLock = new SemaphoreLock(TimeSpan.FromSeconds(30));

        private Queue<TcpRemoteLegacy> disconnectedRemotes = new Queue<TcpRemoteLegacy>();
        private Dictionary<long, TcpRemoteLegacy> connectedRemotes = new Dictionary<long, TcpRemoteLegacy>();

        public int DisconnectedRemoteCount { get { return disconnectedRemotes.Count; } }
        public int ConnectedRemoteCount { get { return connectedRemotes.Count; } }
        public int AcceptableCount { get { return acceptSemaphore.CurrentCount; } }

        public async Task<TcpRemoteLegacy[]> GetAllRemotesAsync()
        {
            using (await semaphoreLock.LockAsync())
            {
                return connectedRemotes.Values.ToArray();
            }
        }

        public TcpServerLegacy(Type remoteType, int remoteMax, int remoteInitialCount = 1000)
        {
            if (remoteInitialCount > remoteMax)
                remoteInitialCount = remoteMax;

            RemoteType = remoteType;
            RemoteMax = remoteMax;

            for (int i = 0; i < remoteInitialCount; i++)
            {
                var remote = CreateRemote();
                disconnectedRemotes.Enqueue(remote);
            }
        }

        ~TcpServerLegacy()
        {
            StopAsync().Wait();
        }

        private TcpRemoteLegacy CreateRemote()
        {
            TcpRemoteLegacy remote = (TcpRemoteLegacy)Activator.CreateInstance(RemoteType);
            remote.Server = this;

            return remote;
        }

        public bool Start(int port)
        {
            try
            {
                if (false == running.Set()) return false;

                Port = port;

                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.NoDelay = true;
                listener.LingerState = new LingerOption(true, 0);

                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(endPoint);
                listener.Listen(100);

                OnStart(listener);

                cancellation = new CancellationTokenSource();
                acceptSemaphore = new SemaphoreSlim(RemoteMax, RemoteMax);

                Task.Run(async () =>
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        var willLog = false;

                        try
                        {
                            await acceptSemaphore.WaitAsync(cancellation.Token);

                            var s = await listener.AcceptAsync();

                            willLog = true;
                            await CheckOutAsync(s);
                        }
                        catch (Exception ex)
                        {
                            if (willLog) log.Error(ex);
                            acceptSemaphore.Release();
                        }
                    }
                }, cancellation.Token);

                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }

        public void Stop(TimeSpan? timeout = null)
        {
            StopAsync(timeout).Wait();
        }

        public async Task StopAsync(TimeSpan? timeout = null)
        {
            if (running.Reset() == false) return;

            if (timeout == null) timeout = TimeSpan.FromSeconds(10);

            cancellation?.Cancel();

            listener.Dispose();
            OnStop();

            var remotes = await GetAllRemotesAsync();
            foreach (var r in remotes)
            {
                try { await r.DisconnectAsync(r.Id); } catch { }
            }

            DateTime outTime = DateTime.Now + timeout.Value;
            while (true)
            {
                if (ConnectedRemoteCount <= 0) break;
                if (DateTime.Now > outTime) break;

                Thread.Sleep(1000);
            }
        }

        public async Task<TcpRemoteLegacy> CheckOutAsync(Socket s)
        {
            var now = DateTime.UtcNow;
            TcpRemoteLegacy remote = null;

            try
            {
                using (await semaphoreLock.LockAsync())
                {
                    if (disconnectedRemotes.TryPeek(out remote))
                    {
                        if (now >= remote.UsableTime)
                            disconnectedRemotes.Dequeue();
                        else
                            remote = null;
                    }

                    if (remote == null)
                    {
                        remote = CreateRemote();
                    }

                    await OnAcceptAsync(remote);

                    await remote.InitializeAsync(s);

                    var remoteId = remote.Id;
                    if (remoteId <= 0)
                        throw new Exception("Already Disconnected");

                    connectedRemotes.Add(remoteId, remote);
                }
            }
            catch (Exception)
            {
                if (remote != null)
                {
                    await remote.DisconnectAsync(remote.Id);
                }

                throw;
            }

            return remote;
        }

        public async Task<bool> CheckInAsync(long remoteId, long userUID)
        {
            using (await semaphoreLock.LockAsync())
            {
                if (connectedRemotes.TryGetValue(remoteId, out var remote))
                {
                    connectedRemotes.Remove(remoteId);
                    disconnectedRemotes.Enqueue(remote);

                    acceptSemaphore.Release();

                    log.Debug("RemoteDisconnected: {0} [ {1}, {2}, {3} ]", userUID, remoteId, ConnectedRemoteCount, DisconnectedRemoteCount);
                    return true;
                }

                return false;
            }
        }

        public async Task<TcpRemoteLegacy> FindAsync(long remoteId, bool needToLock = true)
        {
            using (await semaphoreLock.LockAsync(needToLock))
            {
                if (connectedRemotes.TryGetValue(remoteId, out var remote))
                    return remote;
                else
                    return null;
            }
        }

        public async Task SendAsync(byte[] buffer, int offset, int count, KeyIndex keyIndex)
        {
            await SendAsync(buffer.AsMemory(offset, count), keyIndex);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> memory, KeyIndex keyIndex)
        {
            var remotes = await GetAllRemotesAsync();

            foreach (var r in remotes)
            {
                r.Send(memory, keyIndex, 0);
            }
        }

        public async Task SendWithoutMeAsync(TcpRemoteLegacy me, byte[] buffer, int offset, int count, KeyIndex keyIndex)
        {
            await SendWithoutMeAsync(me, buffer.AsMemory(offset, count), keyIndex);
        }

        public async Task SendWithoutMeAsync(TcpRemoteLegacy me, ReadOnlyMemory<byte> memory, KeyIndex keyIndex)
        {
            var remotes = await GetAllRemotesAsync();

            foreach (var r in remotes)
            {
                if (r != me)
                    r.Send(memory, keyIndex, 0);
            }
        }

        protected virtual void OnStart(Socket socket)
        {
        }
        
        protected virtual void OnStop()
        {
        }

        #pragma warning disable 1998
        protected virtual async Task OnAcceptAsync(TcpRemoteLegacy remote)
        {
        }

        protected virtual async Task OnCheckInAsync(TcpRemoteLegacy remote)
        {
        }
        #pragma warning restore 1998
    }
}
