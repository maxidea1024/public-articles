using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using G.Util;

namespace G.Network
{
    public abstract class TcpRemote : TcpSocket
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        private static readonly int WaitTime = 10_000;

        public TimeBomb TimeBomb { get; set; }
        public TaskTimer AliveTimer { get; set; }

        public long UsableTime { get; set; }

        //internal override async Task InitializeAsync(Socket socket, bool reconnecting)
        //{
        //  //await _semaphoreConn.WaitAsync();
        //
        //  RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
        //
        //  await base.InitializeAsync(socket, reconnecting);
        //}

        public override async Task OnDisconnectAsync(DisconnectReason disconnectReason)
        {
            try
            {
                try { TimeBomb?.Stop(); } catch { }
                try { AliveTimer?.Stop(); } catch { } //todo 이거 사용라나? 재접속상태에서 다시 활성화해야하나?
                UsableTime = SystemClock.Milliseconds + WaitTime;

                await Server.CheckInAsync(SessionId, disconnectReason);
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            finally
            {
                await base.OnDisconnectAsync(disconnectReason);
            }
        }
    }
}
