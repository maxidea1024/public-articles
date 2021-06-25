namespace Prom.Realtime.Server.Internal
{
    //todo Pooling
    internal class LocalEvent
    {
        public LocalEventType Type { get; set; }
        
        public RtStatus Status { get; set; }
        
        public RtRemoteClientInfo ClientInfo { get; set; }

        public IPEndPoint RemoteEndPoint { get; set; }

        public byte[] UserData { get; set; }

        public LinkId GroupId { get; set; }

        public LinkId MemberId { get; set; }

        public int MemberCount { get; set; }

        //Status안에 RemoteID가 이미 있어서 헷갈릴수 있음.
        public LinkId RemoteId { get; set; }

        public SocketError SocketError { get; set; }
    }
}
