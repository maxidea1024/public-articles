namespace Lane.Realtime.Server.Internal
{
    internal enum LocalEventType : byte
    {
        ClientJoinDetermine = 1,
        ClientJoined = 2,
        ClientLeft = 3,
        LogEvent = 4,
    }
}
