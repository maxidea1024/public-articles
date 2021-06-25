namespace Prom.Realtime.Server.Internal
{
    internal enum LocalEventType : byte
    {
        None = 0,
        ClientJoinDetermine = 1,
        ClientJoinApproved = 2,
        ClientDispose = 3,
        Log = 4, // Information/Warning/Error
    }
}
