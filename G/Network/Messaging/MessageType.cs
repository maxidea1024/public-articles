using System;

namespace G.Network.Messaging
{
    public enum MessageType
    {
        None = 0,

        HandshakeReq = 1,
        HandshakeRes = 2,

        User = 100,
    }
}
