## Reliable Session
모바일 환경의 클라이언트와 서버의 TCP 커넥션은 상당히 불안전한 연결형태로 이어져 있습니다. 모바일 이용자는 한곳에 머무르지 않고 이동한다는 특성이 있습니다. 음영지역에 들어가거나, LTE <-> Wifi의 전환이 빈번하게 일어날 수도 있습니다. 이 때 서버와의 연결이 완전히 끊길 수 있습니다.

서버와의 연결이 끊어지게 되면 대개의 게임의 경우 홈화면으로 강제로 이동시켜 버린 후 재접속을 하는 경우가 있습니다. 제일 단순한 방법이기는 하지만, 유저의 플레이 흐름을 깨버리게 되고 접속이 자주 끊기는 상황에서는 게임을 계속 하고 싶은 마음이 없어지게 되는 요인이 됩니다.

홈화면으로 보내지 않고 재빨리 다시 연결하면 플레이를 이어갈수도 있을 것입니다. 하지만, 이때 주의해야할점은 연결이 끊어지기 전후로 메시지가 유실될수도 있고 재연결 후 중복해서 메시지가 보내질수도 있다는 것입니다.

이글에서는 이러한 문제점을 간단히 극복하는 방법에 대해서 설명할것입니다.

### 음영지역(Shadowing Area)
용어의 설명에 대해서는 [여기](http://www.ktword.co.kr/word/abbr_view.php?m_temp1=4196)를 참고하세요.


```cs
public class Message
{
    public int type;
    public uint? seq;
    public uint? ack;
    public ArraySegment<byte> body;
}
```

```cs
public class Session
{
    public Guid sessionId;
    public uint? lastSentAck;
    public uint? lastRecvSeq;
    public uint nextSeq = 1;
    public List<Message> sentMessages;
    public List<Message> unsentMessages;
    public List<Message> pendingMessages;
    public Queue<Message> recvMessages;
}
```

```cs
public class ServerSession : Session
{

}
```

```cs
public class ClientSession : Session
{

}
```

### 구현방법
