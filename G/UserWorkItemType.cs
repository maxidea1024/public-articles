namespace Prom.Realtime.Server.Internal
{
    internal enum UserWorkItemType : byte
    {
        None = 0,
        LocalEvent = 1,
        Rpc = 2,
        Freeform = 3,

        //todo 이건 제거하도록 하자.
        RelayedSend = 4,
    }
}
