namespace Lane.Realtime.Server.Internal
{
    internal enum UserWorkItemType : byte
    {
        LocalEvent,

        RPC,

        Freeform,

        RelayedSend,
    }
}
