namespace Prom.Realtime.Server.Internal
{
    //todo Pooling
    internal class UserWorkItem
    {
        public UserWorkItemType Type { get; private set; }
        public IncomingMessage Message { get; private set; }
        public LocalEvent LocalEvent { get; private set; }
        public byte[] RelayedSendData { get; private set; }
        public LinkId RelayedSendLinkId { get; private set; }

        public UserWorkItem(LinkId relayedSendLinkId, byte[] relayedSendData)
        {
            Type = UserWorkItemType.RelayedSend;
            RelayedSendLinkId = relayedSendLinkId;
            RelayedSendData = relayedSendData;
        }

        public UserWorkItem(IncomingMessage message, UserWorkItemType type)
        {
            Type = type;
            Message = message;
        }

        public UserWorkItem(LocalEvent localEvent)
        {
            Type = UserWorkItemType.LocalEvent;
            LocalEvent = localEvent;
        }
    }
}
