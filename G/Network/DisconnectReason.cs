namespace G.Network
{
    public enum DisconnectReason
    {
        None,
        ByLocal,
        ByRemote,
        SendFailure,
        RecvFailure,
        ConnectFailure,
        Replace,
        GlobalSessionExpired,
    }
}
