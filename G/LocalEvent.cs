namespace Lane.Realtime.Server.Internal
{
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
        public LinkId RemoteId { get; set; }
        public SocketError SocketError { get; set; }
    }
}
