using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using G.Util;

namespace G.Network
{
	public abstract class TcpRemoteLegacy : TcpSocketLegacy
    {
		private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		private static readonly TimeSpan waitingTime = TimeSpan.FromSeconds(10);

		public IPEndPoint RemoteEndPoint { get; private set; }

        // These can be accessed by TcpServerLegacy
		internal TcpServerLegacy Server;

		public TimeBomb TimeBomb { get; set; }
		public TaskTimer AliveTimer { get; set; }

		public DateTime UsableTime { get; set; }

		internal override async Task InitializeAsync(Socket socket)
		{
			await semaphoreConn.WaitAsync();

			RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;

			await base.InitializeAsync(socket);
		}

		protected override async Task OnDisconnectAsync(long remoteId)
        {
			try { TimeBomb?.Stop(); } catch { }
			try { AliveTimer?.Stop(); } catch { }
			UsableTime = DateTime.UtcNow + waitingTime;

			await Server.CheckInAsync(remoteId, userUID);

			await base.OnDisconnectAsync(remoteId);
        }
    }
}
