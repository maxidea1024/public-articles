using System;

namespace G.Network.Messaging
{
    public enum MessageType
    {
        None = 0,

        HandshakeReq = 1,
        HandshakeRes = 2,

        ShutdownTcp = 3,
        ShutdownTcpAck = 4,

        User = 100,

        //todo LocalMessage
    }
}
