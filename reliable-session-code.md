```csharp
public enum State
{
    None,

    // 연결중
    Connecting,

    // TCP 연결이 되지마자 이상태로 전환됨.
    // 최초 암호화 키를 주고 받는 상태
    Handshaking,

    // 핸드쉐이킹 이후 연결이 성립된 상태
    // 암호화 통신 채널까지는 성립됨
    Connected,

    // 만약 이전 상태에서 메시지들을 복원해야할 상황이라면
    // 이 상태로 전환되고 그렇지 않을 경우에는 바로
    // Standby 상태로 전환됨
    WaitForAck,

    // 세션 키를 받을 준비완료
    Standby,

    // 세션키가 온전히 설정된 상태
    Established,
}

void OnConnected()
{
    if (_sessionId.HasValue && IsReliableSession)
    {
        _state = State.WaitForAck;

        if (_lastReceivedSeq != 0)
        {
            SendAck(_lastReceivedSeq + 1, true);
        }
        else
        {
            // flow를 넘어가게 하기 위해서 더미로 보냄.
            SendEmptyMessage();
        }
    }
    else
    {
        OnStandby();
    }
}

bool OnSeqReceived(uint seq)
{
    if (_waitForFirstSeq)
    {
        _waitForFirstSeq = false;
    }
    else
    {
        if (!SeqLess(_lastReceivedSeq, seq))
        {
            Log.Warning($"Last sequence number is {_lastReceivedSeq} but {seq} received. Skipping message.");
            return false;
        }
        else if (seq != _lastReceivedSeq + 1)
        {
            Log.Error($"Received wrong sequence number {seq}. But {_lastReceivedSeq + 1} expected.");
            Disconnect();
            return false;
        }
    }

    _lastReceivedSeq = seq;

    if (_delayedAckInterval <= 0f)
    {
        SendAck(_lastRecevedAck + 1);
    }

    return true;
}

void SendAck(uint32 ack)
{
    var msg = new Message(MessageType.Ack);
    msg.Ack = ack;
    SendMessage(msg);
}

void OnDelayedAckEvent(float deltaTime)
{
    if (SeqLess(_sentAck, _lastReceivedAck + 1))
    {
        SendAck(_lastReceivedAck + 1);
    }
}

void OnAckReceived(uint32 ack)
{
    if (!IsConnected)
    {
        return;
    }

    lock (_myLock)
    {
        while (_sentQueue.Count > 0)
        {
            Message msg = _sentQueue.Peek();

            if (SeqLess(msg.Seq, ack))
            {
                _sentQueue.Dequeue();
            }
            else
            {
                break;
            }

            if (_state == State.InitialWaitForAck)
            {
                if (_sentQueue.Count > 0)
                {
                    Log.Debug($"Ack: {ack}, SentQueue: {_sentQueue.Count}");

                    foreach (Message msg in _sentQueue)
                    {
                        if (msg.Seq == ack || SeqLess(msg.Seq, ack))
                        {
                            SendMessage(msg);
                        }
                        else
                        {
                            // Wrong
                        }
                    }
                }

                OnStandby();
            }
        }
    }
}

uint GetNextSeq()
{
    return _nextSeq++;
}

static bool SeqLess(uint x, uint y)
{
    // http://en.wikipedia.org/wiki/Serial_number_arithmetic
    return (int)(y - x) > 0;
}
```
