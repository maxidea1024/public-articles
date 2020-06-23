# TCP에서 신뢰성 있는 메시지 처리

모바일 네트워크 처럼 연결 끊김이 빈번한 환경에서는 안정적인 게임 서비스를 제공하려면, 추가적인 고려사항이 있습니다.

크게 두가지가 있습니다.

- 네트워킹 시 배터리 소모
- 불안정한 네트워크

네트워킹시에 예상 보다는 다소 상회하는 배터리 소모가 발생합니다. 최대한 적은 데이터를 주고 받으면 완화시킬수는 있겠지만, 완벽하게 대응할 수는 없습니다. 최대한 전송되는 데이터를 줄이고 횟수를 줄이는것 외에는 별다른 방법이 없습니다.

다음으로 모바일 네트워킹 환경은 `PC 네트워킹` 환경과는 다르게 매우 불안정합니다. `PC`에서도 무선 공유기등을 사용하기는 하지만, `PC`는 고정된 환경에서 사용하고 모바일은 이동중에 사용하기 때문에 많이 좋아졌다고는 하나 물리적인 특성으로 인해서 불안정한 환경에 자주 노출되곤 합니다.

모바일에서는 TCP 연결이 자주 끊기곤합니다.

끊어진 TCP 연결을 다시 맺는것은 어려운 일이 아니지만, 재연결 중 유실된 메시지를 복원하는 것은 쉬운 과정이 아닐 수 있습니다. 더욱이 이러한 처리를 응용레벨에서 처리하는것은 프로그램을 지나치게 복잡하게 만들수 있습니다. 또한 응용레벨의 작업자들은 네트워크 숙련자들이 아닐수 있으므로 그 부담을 지게 하는것은 바람직하지 않을것입니다.

재접속 과정에서 유실된 메시지를 복원하지 못하고 그대로 내버려둘 경우에는 게임의 상태가 정상적이지 않을 수 있으므로, 재연결 후에 게임을 이어서 할 수 없을수 있습니다. 보통 이러한 경우에 가장 손쉬운 방법은 접속이 끊기면 강제로 `홈`으로 보내버리고 재접속을 강제할 수도 있습니다. 하지만, 플레이어는 이러한 과정을 불편하게 생각할것이고 게임의 흐름을 이어갈 수 없는 상황이 될것입니다.

더욱이 모바일 환경에서는 이러한 순간적인 끊김이 빈번하게 발생한다는 것입니다. 수초 이내의 짧은 연결 끊김으로 인해서 매번 홈 화면으로 보내버리면, 플레이어는 안정적인 게임서비스를 받고 있다는 생각을 하지 못할것입니다. 플레이어의 네트워크 환경이 열악하다는 생각은 하지 못한채로 불만만 쌓여 갈것입니다.

가장 확실한 방법은 역설적이게도 실시간 네트워킹을 사용하지 않는 것입니다. 기획적으로 비동기 네트워크 즉, 웹 서버로도  충분하다면 재시도 로직과 응답 캐싱을 통한 중복 처리 방지 정도만 할 수 있다면, 이게 훨씬더 손쉬운 방법일 것입니다.

기획 내용상 실시간 네트워킹 요소가 필수라면 아래의 내용들을 한번 살펴보는것도 좋을것 같습니다.

이 글에서는 이러한 상황을 개선하기 위한 방법에 대해서 설명하고자 합니다.

## 연결이 끊기는 이유

우선 자세한 설명에 앞서 모바일 네트워크 환경의 특성에 대해서 알아 보겠습니다. 모바일은 `PC` 네트워크 환경과 달리 전적으로 무선 환경입니다. 무선 환경은 유선에 비해 상대적으로 매우 불안정한 채널이며 언제든 연결이 끊길 수 있습니다. 특히 이동중에는 `WiFi` <-> `LTE` 로의 전환이 빈번하게 발생할수도 있습니다. 전환이 일어나게 되면 한동안은 통신이 이루어지 않으며, 절체 후에는 네트워크 연결이 끊김은 물론이고, 기기의 `IP` 주소가 변경되는 등의 변화가 있을 수 있습니다.

바로 이러한 부분에 대한 대비를 해두어야만 원활한 모바일 네트워크 서비스를 할 수 있을것입니다.

## 극복이 어려운 점

하지만, 이러한 처리가 모든 상황을 극복하지는 못합니다. 다음의 상황에서는 매끄러운 처리가 어려울 수 있습니다. 

- `WiFi` 수신 감도가 낮아졌을 경우, 커넥션이 끊김을 감지할 때까지 상당시간 소요됨.
- `LB(Load Balancer)` 뒤에 서버가 위치해 있을 경우.

위 문제들에 대해서는 아래에서 좀더 자세하게 다루겠습니다.

### WiFi 수신 감도가 낮아졌을 경우

`WiFi` 수신 감도가 낮아진 경우에는 이게 실제로 끊기기까지 시간이 필요하다는게 문제입니다. 실제로 끊긴건지 일시적으로 낮아졌다가 다시 원래대로 돌아오는 것인지 인식하는데 다소의 시간이 걸리기 때문에 지연은 불가피하게 발생합니다. 반대로 바로 연결이 끊어지고 재빠르게 다시 접속하는게 유리하다고 판단할수도 있겠지만, 연결 과정은 다소 복잡한 과정을 거치기 때문에 잦은 재접속이 꼭 유리한것만은 아닐것입니다. 대개의 경우 실제 연결이 끊겼는지 여부를 판단하는 임계치(timeout)이 있기 마련인데 이 값이 민감하게 반응하는것을 피하기 위해서 큰 값으로 설정되어 있곤합니다. 이러한 이유로 어느정도 시간이 흐른 뒤에서 실제 접속이 끊겼음을 감지하게 됩니다. 사용자는 이 시간동안 멍하게 있을수 밖에 없을것입니다.

### `LB(Load Balancer)` 뒤에 서버가 위치해 있을 경우

부하 분산을 위해서 보통 서버들을 `LB(L4)` 뒤에 위치시킵니다. 보통의 웹서버 경우에는 문제가 없지만, 상태를 가지고 있는 서버의 경우에는 재접속시에 문제가 발생합니다. 접속이 끊기기 전의 서버에 상태가 있었을테고, 상태를 이어가기 위해서는 이전 서버로 접속을 해야할것입니다.

세션 혹은 게임의 상태가 이전 서버의 메모리등에 있을테니, 반듯이 이전 서버로 접속을 해야하는데 `LB` 뒤에 서버가 위치해 있기 때문에 다른 서버에 접속하게 되면, 재접속이 아닌 최초 접속으로 인식하게될 것이고, 상태를 이어갈 수 없을 것입니다. 이를 해결하는 가장 간단한 방법은 각 서버에 `Public IP`를 부여하고 최초 접속시에는 `LB`를 통해서 접속하고 재접속시에는 이번에 접속했던 서버의 `Public IP`로 접속을 하도록 하면 해결할 수 있습니다.

각 서버들마다 `Public IP`를 부여해야하는 부담은 발생하지만 실제로 `Public IP` 대여 비용은 크지 않은 편이니 크게 문제가 될 부분은 아니라고 생각됩니다. 아니면, 전용 `LB` 솔루션을 개발해서 처리할 수도 있을것입니다.

## 네트워크 단절시 발생하는 상황

간단한 게임내에서의 상황을 생각해 보도록 하겠습니다.

![전송중 연결 끊김](https://i.loli.net/2020/06/23/WzI1T56mLveUf2u.png)

클라이언트는 서버에게 `불멸의 검`을 장착해달라고 했고, 실제로 서버는 `불멸의 검` 장착 요청을 처리했고 그 결과를 클라이언트에게 돌려 주었습니다. 하지만, 그림에서 보는 바와 같이 처리는 했지만 클라이언트에게 응답을 보내는 과정중에 접속이 끊겨버리는 상황이 발생했습니다. 클라이언트는 `불멸의 검`이 실제로 장착되었는지 여부를 알 수 없습니다. 클라이언트는 권한이 없으므로 요청만 할 수 있기에 서버내의 객체(세션)에는 `불명의 검`이 장착된걸로 간주하고 처리되겠지만, 클라이언트에 보이는 플레이어 객체는 `불명의 검`을 장착하지 않은채로 공격을 하게 될것이며, 이미 네트워크가 끊긴 상태이므로 실제로 몹에게 데미지가 들어가지 않는 현상이 발생할것입니다.

이러한 상황에서 메시지 복원없이 재접속을 하게되면 메시지 유실이 발생하므로, 위의 상황이 해결되지 않는 문제는 여전합니다.

![재연결](https://i.imgur.com/q6AMEoO.png)

아예 홈으로 돌아가서 재접속을 하고 플레이어 정보 전체를 다시 받으면, `불멸의 검`은 장착이 되어 있겠지만, 플레이 흐름은 이어갈 수 없을 것이며, 마지막으로 몹에게 공격한 행위는 반영되지 않을 것입니다.

이러한 문제를 해결하기 위해서는 메시지 유실이 발생하지 않도록 네트워크 엔진에서 처리하는게 바람직할것입니다.

## 메시지 유실 방지

위에서 본대로 갑작스런 연결끊김으로 인해서 메시지의 유실이 발생하게 됩니다. 요행으로 끊기기 전후로 아무런 메시지도 전송하지 않았다면, 문제가 없겠지만 게임처럼 수시로 다수의 메시지가 오고가는 환경에서는 필연적으로 메시지 유실이 발생할것이고 이로인해서 클라이언트, 서버가 생각하는 상태의 불일치로 인해서 게임의 상태는 급속도로 엉망이 될것입니다.

자, 이 문제를 어떻게 해결해야할지에 대해서 이제 본격적으로 알아보도록 하겠습니다.

아이디어는 간단합니다. 송신측에서 보낸 메시지를 수신측에서 수신했다고 알려주기 전까지는 버리지 않고, 보관했다가 연결유지를 위한 재접속시에 상대측이 미쳐 수신하지 못했던 메시지를 모두 보내주면 됩니다.

자 여기서 상대측이 수신했는지 여부를 어떻게 알수 있을지에 대한 의문이 들것입니다. 쉽게 난 `500번 메시지`까지 받았으니 `501번 메시지`부터 보낼거 있으면 보내주도록 처리하면 됩니다.

메시지를 보낼때마다 메시지에 번호를 부여하고 이 메시지를 받은 상대측은 `받은 메시지 번호 + 1`를 `응답신호(ACK)`로 보내주면 됩니다. 응답신호를 받은 송신자는 보관하고 있던 메시지들중에서 응답번호다 작은 번호의 메시지들을 제거해주는 형태로 처리하면 됩니다.

## 구현

설명을 위해서 다음과 같이 간단히 몇가지를 정의하도록 하겠습니다.

#### MessageType

```csharp
public enum MessageType
{
    None = 0,
    Empty = 1,
    Handshake = 2,
    Handshake2 = 3,
    Ping = 4,
    User = 5,
}
```

| 이름 | 설명 |
|:--|:--|
|None|정의되지 않은 메시지입니다.|
|Empty|비어있는 메시지입니다. 단순히 `Ack`를 담아서 보내거나 흐름 전환을 위해서 사용되는 메시지입니다|
|Handshake|암호화된 통신을 하기 위해서 암호화키 교환용 메시지입니다.|
|Handshaking2|상대방의 공개키로 암호화된 대칭키를 보내는 메시지입니다.|
|Ping|연결 유지 및 `Round Trip Time` 측정을 위한 Ping 메시지입니다.|
|User|실제 주고받는 유저 메시지입니다.|

#### Message

```csharp
public class Message
{
    public MessageType Type;
    public uint? Seq;
    public uint? Ack;
    public Guid? SessionId;
    public bool IsEncoded;
    public ArraySegment<byte> Body;
}
```

|이름|설명|
|--|--|
|Type|메시지 타입입니다.|
|Seq|메시지 일련번호입니다.|
|Ack|메시지 수신 응답 번호입니다.|
|SessionId|세션 식별을 위한 Session ID입니다.|
|IsEncoded|메시지 내용 인코딩 여부입니다.|
|Body|메시지 페이로드. 인코딩된 유저 메시지 데이터입니다.|

#### SessionState

```csharp
public enum SessionState
{
    None = 0,
    Connecting = 1,
    Handshaking = 2,
    Connected = 3,
    InitialWaitForAckForRecovery = 4,
    Standby = 5,
    Established = 6,
}
```

|이름|설명|
|--|--|
|None|최초 상태(접속이 끊어진 상태)|
|Connecting|연결중|
|Handshaking|암호화키 교환|
|Connected|연결됨(단순히 암호화된 메시지를 주고 받을 수 있는 상태로, 아직 유저 메시지를 보낼수는 없음)|
|InitialWaitForAckForRecovery|재접속 후 상대측의 메시지 복원처리를 위해서 `ACK`를 기다림|
|Standby|연결완료|
|Established|세션이 정상적으로 성립(Establishment) 되었음|

#### Session

```csharp
public class Session
{
    public SessionState State;
    public byte[] PublicKey;
    public byte[] PrivateKey;
    public byte[] EncryptionKey;
    public Guid? SessionId;
    public uint? LastSentAck;
    public uint? LastRecvSeq;
    public uint NextSeq;
    public Queue<Message> SentMessages;
    public List<Message> UnsentMessages;
    public List<Message> ReceivedMessages;
    public List<Message> PreferredSendMessages;
    public List<Message> PendingSendMessages;
    public List<Message> SendingMessages;
}
```

|이름|설명|
|:--|:--|
|State|현재 세션 상태입니다|
|PublicKey|대칭키 교환을 위해서 사용되는 공개키입니다.|
|PrivateKey|대칭키 교환을 위해서 사용되는 비밀키입니다.|
|EncryptionKey|암호화를 위해서 사용되는 대칭키입니다.|
|SessionId|세션 구분을 위한 세션키(UUID)입니다.|
|LastSentAck|마지막으로 보낸 메시지 수신 응답 번호입니다.|
|LastRecvSeq|마지막으로 수신받은 메시지 번호입니다.|
|NextSeq|다음에 보내는 메시지 번호입니다.|
|SentMessages|송신한 메시지 보관 목록으로 수신측에서 정상 수신했다고 알리기 전까지 보관을 위해서 사용됩니다.|
|UnsentMessages|최종적으로 연결이 Establish된 이후에 송신할 수 있으므로, 메시지 유실을 방지하기 위한 메시지 보관 목록입니다.|
|PreferredSendMessages|세션 성립 이전에라도 전송이 되어야하는 메시지들입니다.|
|PendingSendMessages|메시지 송신시 일차로 메세지들은 이 목록에 담기게 됩니다.|
|SendingMessages|현재 IO에서 보내지고 있는 메시지 목록입니다. (PreferredSendMessage 혹은 PendingSendMessages 둘중에 하나입니다.)|

간단하게 필요한 요소들을 정의해 보았습니다. 이제 하나씩 구현해보도록 하겠습니다. 아래 구현 코드들은 실제로 동작하는 코드가 아니므로, 그냥 참고용으로만 보시면 되겠습니다.

실제로 구현 코드를 작성한다고 해도 오류 처리나 소켓단의 송수신 관련 부분과 메시지 시리얼라이징만 구현해주면 되므로 어느정도 파악하는데는 문제가 없을거라고 생각합니다.

### 구현 코드에 앞선 몇가지 고려사항

- 각 상태별로 `timeout`를 해주어야합니다.
  상대측에서 응답을 주지 않는 일은 허다하기 때문입니다. 무한정 기다리다 보면 사용하지 않는 쓰레기 객체가 서버에 쌓이기 때문에 심각한 문제를 야기할 수 있습니다.

- `ACK`를 매 메시지 수신 할때마다 보내야하나?
  아닙니다. 가끔 보내줘도 아무런 문제가 없습니다. 다만, 보냈으나 아직 응답을 받지 못한 메시지 목록이 비대해지는 문제가 발생할 수 있겠지만, 적당한 주기로 응답을 주는 형태로 구현한다면 네트워크 동작도 줄이고 원하는 결과를 얻을 수 있을것입니다.

- 재연결시에 `IP`가 수시로 바뀔수 있으므로 `IP`를 세션을 구분짓기 위한 키로 사용하면 안됩니다.

### 연결

우선 TCP 연결을 하도록 하겠습니다. 그냥 뭐 별거 없습니다. TCP socket으로 연결하는 과정이라고 생각하면 됩니다.

```csharp
session.Connect("211.223.100.22:50000");
```

#### TCP 연결이 완료 되었을때

```csharp
void Session.OnTcpConnected()
{
    // 메시지 암호화에 사용되는 대칭키 교환을 위해서
    // 사용되는 공개키 / 비밀키를 생성합니다.
    GeneratePublicAndPrivateKey(out PublicKey, out PrivateKey);

    // 공개키를 상대방에게 보냅니다.
    var handshaking = new HandshakingMessage();
    handshaking.PublicKey = PublicKey;
    SendMessagePreferred(handshaking);

    // 암호화 키를 교환하기 위한 상태로 변경합니다.
    State = State.Handshaking;
}
```

### TCP 연결이 끊어졌을때

```csharp
void Session.OnTcpDisconnected()
{
    // `SessionId` 값이 유효하다는 것은 끊어지기 이전에 세션이 성립되어
    // 있다는 얘기이므로, 연결 복원을 위해 자동으로 재연결을 시도합니다.

    if (SessionId.HasValue)
    {
        Reconnect();
    }
    else
    {
        // 일반적인 접속 끊김 처리는 여기서 해주면 됩니다.
    }
}
```

#### 메시지를 받았을때

```csharp
void Session.OnMessageReceived(Message message)
{
    if (message.Type == MessageType.Empty)
    {
        // `Seq` 필드가 지정 되어 있을 경우
        if (message.Seq.HasValue)
        {
            OnSeqReceived(message.Seq.Value);
        }

        // `Ack` 필드가 지정되어 있을 경우
        if (message.Ack.HasValue)
        {
            OnAckReceived(message.Ack.Value);
        }

        // `SessionId` 필드가 지정되어 있을 경우
        if (message.SessionId.HasValue)
        {
            OnSessionIdReceived(message.SessionId.Value);
        }

        return;
    }

    switch (message.Type)
    {
        case MessageType.Handshaking:
            OnHandshakingMessageReceived(message.DeserializeBody<HandshakingMessage>());
            break;
        case MessageType.Handshaking2:
            OnHandshaking2MessageReceived(message.DeserializeBody<Handshaking2Message>());
            break;
        case MessageType.Ping:
            OnPingMessageReceived(message.DeserializeBody<PingMessage>());
            break;
        case MessageType.User:
            OnUserMessageReceived(message);
            break;
        default:
            throw new NotSupportedException($"Invalid message received. type: {message.Type}");
    }
}
```

#### 메시지 보내기

이 함수는 기본적으로 세션이 성립되기 전에는 메시지 송신을 하지 않고, 대기 목록에 담아두고 세션이 성립되는 시점에서 일괄적으로 전송합니다. 만약, 세션이 성립되기 전에 메시지를 보내야할 경우에는 `isPreferredSend` 플래그를 `true`로 설정하거나 `SendMessagePreferred` 함수를 사용해야합니다.

```csharp
void Session.SendMessage(Message message, bool isPreferredSend = false)
{
    // 사용자 메시지가 아닌 경우에는 `Seq`를 부여하지 않습니다.
    if (message.Type == MessageType.User)
    {
        // Seq가 지정되지 않은 경우에만 Seq를 지정합니다.
        if (!message.Seq.HasValue)
        {
            message.Seq = GenerateNextSeq();
        }
    }

    // 우선적으로 보낼 메시지가 아니면 미뤄뒀다가 세션이 성립되면
    // 일괄적으로 몰아서 보내도록 합니다.
    if (!isPreferredSend && State != State.Established)
    {
        UnsentMessages.Add(message);
        return;
    }

    // 사용자 메시지만 복원의 대상이 됩니다.
    if (message.Type == MessageType.User)
    {
        // 차후 메시지 복원을 위해서 보관해둡니다.
        // Ack를 받은 후에야 안전하게 제거될 수 있습니다.
        SentMessages.Enqueue(message);
    }

    // 보낼 메시지 목록에 넣어줍니다.
    PendingSendMessages.Add(message);

    // 보내기가 가능한 상태라면 바로 보냅니다.
    SendPendingMessages();
}
```

#### 세션 성립전에라도 메시지 보내기

```csharp
void Session.SendMessagePreferred(Message message)
{
    SendMessage(message, true); // preferred
}
```

#### `NextSeq` 생성하기

순차적으로 단조 증가하는 값을 생성합니다.

```csharp
uint Session.GenerateNextSeq()
{
    return NextSeq++;
}
```

#### Handshaking 메시지를 받았을때 호출되는 함수

```csharp
void Session.OnHandshakingMessageReceived(HandshakingMessage message)
{
    // 받은 암호화키(공개키)를 가지고 생성된 대칭키를 암호화하여 보내줍니다.
    var secret = GenerateEncryptionKey();
    
    // 상대방의 공개키로 암호화 / 복호화에 사용되는 대칭키를 암호화하여 전송합니다.
    var handshaking2 = new Handshaking2Message();
    handshaking2.EncryptionKey = EncryptByPublicKey(secret, message.PublicKey);
    SendMessagePreferred(handshaking2);

    // 암호화 키를 교환했으므로, 연결된 상태로 전환합니다.
    State = State.Connected;
}
```

#### Handshaking2 메시지를 받았을때 호출되는 함수

```csharp
void Session.OnHandshaking2MessageReceived(HandshakingMessage2 message)
{
    // 받은 대칭키를 수신측의 비밀키로 복호화합니다.
    var encryptionKey = DecryptByPrivateKey(message.EncryptionKey);

    // 복호화된 대칭키를 보관해둡니다.
    EncryptionKey = message.EncryptionKey;

    // 메시지 복원 시작
    OnRecovery();   
}
```

#### Handshaking 완료후에 호출되는 함수

```csharp
void Session.OnRecovery()
{
    if (SessionId.IsValid)
    {
        // 이전에 가지고 있던 SessionId가 유효하다는 것은, 접속이 이루어졌었다는 얘기이므로
        // 메시지 복원을 위해서 상대방의 Ack를 기다리는 상태로 전환하고
        // 상태방에게 Ack를 보내주어서 혹시 보내지 못한 메시지가 있으면 보내주도록 합니다.
        // 이부분이 재연결 후 메시지 복원을 하는 핵심 코드입니다.

        State = State.InitialWaitForAckForRecovery;

        if (LastRecvSeq.HasValue)
        {
            SendAck(LastRecvSeq.Value +1, true); // preferred send
        }
        else
        {
            // 재접속이 아닌 최초 접속이라면 다음 상태로 넘어가기 위해서 그냥 빈 메시지를 보내는데
            // 이를 받은 서버는 `EmptyMessage`에 SessionId 실어서 보내주게 됩니다.
            // 이 SessionId를 받게 되면 최종적으로 세션이 성립되고 정상적으로 유저 메시지들을
            // 주고 받을 수 있는 상태가 됩니다.

            var message = new EmptyMessage();
            SendMessagePreferred(message);
        }
    }
    else
    {
        OnStandby();
    }
}

// 서버는 `EmptyMessage`를 세션에 연결된 `SessionId`를 클라이언트에게 보내줍니다.
void ServerSession.OnEmptyMessageReceived(EmptyMessage message)
{
    EmptyMessage message2 = new EmptyMessage();
    message2.SessionId = this.SessionId;
    SendMessage(message2);
}
```

#### 세션키를 받을 준비가 되었을때

```csharp
void Session.OnStandby()
{
    // 준비 상태로 전환함.
    State = State.Standby;

    if (!SessionId.HasValue)
    {
        // 서버에게 `EmptyMessage`를 보내서 `SessionId`를 발급해줄것을 요청합니다.
        var message = new EmptyMessage();
        SendMessagePreferred(message);
    }
    else
    {
        // 세션이 성립된걸로 간주합니다.
        State = State.Established;
    }
}
```

#### `Seq`를 받았을때

```csharp
bool Session.OnSeqReceived(uint seq)
{
    if (LastRecvSeq.HasValue)
    {
        // 이전에 받았던 `Seq`가 있다면, 새로 받은 `Seq`는 이전에 받은 `Seq + 1`이 되어야할 것입니다.
        // 그렇지 않다면, 해킹이나 프로그램 오류일 가능성이 높습니다.

        if (seq != (LastRecvSeq.Value + 1)) // overflow가 발생해도 단순 비교이므로 문제 없습니다.
        {
            Disconnect(DisconnectReason.BadSeq); // Disconnect 사유로 잘못된 `Seq` 번호 때문임을 지정합니다.
            return false;
        }
    }

    // 마지막으로 수신한 메시지 번호를 기록해둡니다.
    LastRecvSeq = seq;

    // 송신자측에서 메시지 정상 수신 여부를 알수 있도록 응답을 보내줍니다.
    // 통상적으로 받은 메시지 번호 + 1을 해서 보냅니다.
    // (TCP 의 ACK와 동일함)
    SendAck(LastRecvSeq + 1);

    return true;
}
```

```csharp
void Session.SendAck(uint ack, bool preferredSend = false)
{
    // 마지막으로 송신한 `Ack` 번호를 기록해둡니다.
    LastSentAck = ack;

    // 빈 메시지에 `Ack` 필드만 설정해서 보내줍니다.
    var message = new EmptyMessage();
    message.Ack = ack;
    SendMessage(message, preferredSend);
}
```

#### `Ack`를 받았을때

```csharp
void Session.OnAckReceived(uint ack)
{
    if (!IsConnected)
    {
        // 연결되어 있는 상태가 아니면 어짜피 메시지를 전송할 수 없으므로,
        // 바로 리턴합니다.
        return;
    }

    // 상대측에서 정상적으로 수신한 메시지들을 `SentMessage` 목록에서 제거 합니다.
    while (SentMessages.Count > 0)
    {
        var message = SentMessage.Peek();
        if (SeqNumberHelper.Less(message.Seq, ack)) // overflow 이슈를 피하기 위해서 별도의 헬퍼 함수를 사용하여 대소 구분을 해야합니다.
        {
            // 상대측에서 이미 수신한 메시지이므로, 제거해도 안전합니다.
            SentMessages.Dequeue();
        }
        else
        {
            // 이메시지 이후로는 상대측에서 아직 수신확인이 안되었으므로 조금더 보관해야합니다.
            break;
        }
    }

    // 재접속 이후 메시지 복원 상태중이라면 상대측이 수신인정하고 남은 메시지들을 일괄 재전송해줍니다.
    if (State == State.InitialWaitForAckForRecovery)
    {
        foreach (var message in SentMessages)
        {
            if (SeqNumberHelper.LessOrEqual(ack, message.Seq))
            {
                SendMessage(message);
            }
        }

        // Standby 상태로 전환합니다.
        OnStandby();
    }
}
```

#### `SessionId`를 받았을때

```csharp
void Session.OnSessionIdReceived(Guid sessionId)
{
    if (State != State.Standby)
    {
        return;
    }

    // 세션 ID가 이전 값과 다를 경우에 `NextSeq` 변수를 임의 값으로 초기화합니다.
    // 보안상 유리한 측면이 있어서 임의의 값으로 하는 것일뿐 0이 아닌 임의의 값으로
    // 설정해도 상관 없습니다.
    if (this.SessionId == null || this.SessionId.Value != sessionId)
    {
        this.SessionId = sessionId;

        var rng = new System.Random();
        NextSeq = (uint)rng.Next();
    }

    // 세션이 성립되었습니다.
    // 이제부터 유저 메시지를 바로바로 송수신할 수 있는 상태가 되었습니다.
    State = State.Established;

    // 전송 대기중인 메시지들을 일괄로 전송합니다.
    SendUnsentMessages();
}
```

#### 연결 성립전에 전송 요청된 메시지 일괄 전송

```csharp
void Session.SendUnsentMessages()
{
    // 세션 성립전에 전송 요청된 메시지들을 일괄적으로 전송합니다.
    if (UnsentMessages.Count > 0)
    {
        foreach (var message in UnsentMessages)
        {
            SendMessage(message);
        }

        UnsentMessages.Clear();
    }

    // 네트워크 너머로 메시지들을 전송합니다.
    // 이미 전송중인 메시지가 있다면, 전송이 모두 완료된 후에 콜백됩니다.
    // 즉, 전송을 다 마친 후 자동으로 다음 전송이 이루어집니다.
    SendPendingMessagesToWire();
}
```

#### 명시적으로 연결 끊기

```csharp
void Session.Disconnect()
{
    if (State == State.None)
    {
        return;
    }

    State = State.None;
    SessionId = null;
    LastRecvSeq = null;
    LastSentAck = null;
    SentMessages.Clear();
    UnsentMessages.Clear();

    // TCP 소켓을 닫아줍니다.
    TcpSocket.Close();
}
```

#### 실질적인 메시지 전송

```csharp
void Session.SendPendingMessagesToWire()
{
    // 연결이 끊어졌거나, 비동기로 이미 보내고 있는 중일 경우에는 전송을 할 수 없습니다.
    // 전송이 완료되면 이 함수가 콜백 되므로 연이어서 송신 처리가 이루어지게 됩니다.
    if (!IsSendable)
    {
        return;
    }

    // 보낼 메시지가 없는데 호출된 경우에는 아래 동작을 수행하지 않고 바로 반환합니다.
    if (PreferredSendMessages.Count == 0 && PendingSendMessages.Count == 0)
    {
        return;
    }

    // `PreferredSendMessages`에는 세션 성립전에도 전송되어야 하는 메시지들이
    // 담겨져 있습니다. 이 목록에 있는 메시지들을 먼저 전송해야합니다.
    // `PreferredSendMessages` 전송이 완료된 시점에서 이 함수가 콜백되므로
    // `PendingSendMessages`도 자동으로 전송을 시작하게 됩니다.

    List<Message> tmp = SendingMessages;

    if (PreferredSendMessages.Count > 0)
    {
        SendingMessages = PreferredMessages;
        PreferredMessages = tmp;
    }
    else
    {
        if (!SessionId.HasValue)
        {
            return;
        }

        SendingMessages = PendingSendMessages;
        PendingSendMessages = tmp;
    }

    foreach (var message in SendingMessages)
    {
        if (!message.IsEncoded)
        {
            // 메시지 인코딩이 되어 있지 않을 경우에는 인코딩을 해주어야 네트워크로 전송가능한 형태가 됩니다.
            EncodeMessage(message);
        }
        else
        {
            // 재접속 이후 암호화키가 변경되는데, 이미 인코딩이 된 메시지라면 암호화키 변경 이슈로 인해서
            // 다시 인코딩해야 수신측에서 정상적인 메시지로 인식할 수 있습니다.
            ReencodeMessage(message);
        }
    }

    // 요청한 내용(SendingMessages)에 해당하는 메시지들을 네트워크 너머로 전송합니다.
    IssueWireSend();
}
```

#### 메시지 인코딩

```csharp
// 메시지 내용을 인코딩 합니다.
// - 대칭키로 암호화하거나 압축등의 과정을 거치고 최종적으로 바이트 또는 base64 형태의 텍스트로 인코딩 합니다.
void Session.EncodeMessage(Message message)
{
    message.Encoded = true;

    .
    .
    .
}

// 재접속 이후 암호화키가 변경되므로 재전송시에 변경된 암호화키로 다시 인코딩해야 수신측에서
// 정상적인 메시지로 인식할 수 있습니다.
void Session.ReencodeMessage(Message message)
{
    .
    .
    .
}
```

위 코드에서 사용된 `SeqNumberHelper` 클래스는 [여기](serial-number-arithmetic.md)를 참고하세요.
