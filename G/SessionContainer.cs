
public SessionContainer()
{
    _sessions = new ConcurrentDictionary<string, IAppSession>(StringComparer.OrdinalIgnoreCase);
}

public override ValueTask<bool> RegisterSession(IAppSession session)
{
    if (session is IHandshakeRequiredSession handshakeSession)
    {
        if (!handshakeSession.Handshaked)
            return new ValueTask<bool>(true);
    }

    session.Closed += OnSessionClosed;
    _sessions.TryAdd(session.SessionId, session);
    return new ValueTask<bool>(true);
}

public int SessionCount => _sessions.Count;

public IEnumerable<IAppSession> GetSessions(Predicate<IAppSession> criteria = null)
{
    var enumerator = _sessions.GetEnumerator();

    while (enumerator.MoveNext())
    {
        var s = enumerator.Current.Value;

        // 연결중이 아닌 세션은 제외함.
        if (s.State != SessionState.Connected)
            continue;

        if (criteria == null || criteria(s))
            yield return s;
    }
}


public class ServerOptions
{
    public void AddListener(ListenerOptions listenerOptions)
    {
        //todo
    }
}


/*

await host.RunAsync();

*/

//todo WebSocket도 지원하도록 하자.

public class MyRequestInfo : IRequestInfo
{
    public string Key { get; set; }

    public int DeviceId { get; set; }
}

protected override void OnSessionStarted()
{
    //
}

protected override void OnSessionClosed(CloseReason reason)
{
    //
}

static void AppServer_SessionClose(AppSession session, CloseReason reason)
{
    //
}

var session = server.GetSessionById(sessionId);
if (session != null)
    session.Send(data, 0, data.Length);

foreach (var session in server.GetSessions())
    session.Send(data, 0, data.Length);

// IConnectionFilter

// TLS도 지원해야할까? 아니면 자체적인 핸드쉐이킹을 통해서 처리하면 될까?

// 서버 관리자
// 유저 관리자
// 서버간 이동 처리를 하도록 하자.

// 서버이름
// 서버타입
// 서버타입네임
// 서버포트
// 수신대기로그크기
// 모드(Tcp, Udp)
// 송신타임아웃
// 송싱큐크기
// 최대연결수
// 수신버퍼크기
// 송신버퍼크기
// 동기화모드로데이터보내기
// 커맨드로그여부
// 유휴세션제거하기여부
// 유휴세션제거타임아웃
// TLS사용여부
// 비밀키(이게 지정되어 있으면 비밀키 교환을 하지 않음)
// 세션스냅샷여부
// 인증서(TLS를 사용하는 경우)

// 스레드풀최대작업수
// 스레드풀최소작업수
// 스레드풀최대IO워커수
// 스레드풀최소IO워커수
// 성능데이터수집여부
// 성능데이터수집간격
// 격리수준
// 로그팩토리
// 기본컬쳐
