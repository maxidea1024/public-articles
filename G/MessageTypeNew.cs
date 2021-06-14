using System;

namespace G.Network.Messaging
{
    public enum MessageType
    {
        None = 0,

        ClientHello = 1,
        ServerHello = 2,

        ClientHandshake = 3,
        ServerHandshake = 4,

        //todo 사실 이 타입은 필요없음.
        Syn = 5,

        RequestEstablishment = 6,
        Established = 7,

        ShutdownTcp = 10,
        ShutdownTcpAck = 11,

        User = 100,



        //todo 제거
        HandshakeReq = 33,
        HandshakeRes = 34,
    }
}
