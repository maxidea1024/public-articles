using System;

namespace G.Network.Messaging
{
    public enum NetworkResult : byte
    {
        OK = 0,

        // 재연결중 서버내에서 이미 세션 컨텍스트가 폐기된 경우.
        // 특정 시간내에 재연결을 성공하지 못한 경우에 발생할 수 있음.
        // 혹은, 서버에서 임의로 연결을 종료한 경우에 컨텍스트가 이미
        // 파괴되었을 경우에 해당.
        ContextNotFound = 33,
        ContextExpired = 34,
    }
}
