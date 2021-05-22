public enum State
{
	kUnknown = 0,
	kConnecting,
	kHandshaking,
	kConnected,
	kWaitForAck,
	kStandby,
	kEstablished
};

bool IsConnected
{
	get
	{
		lock (_socketLock)
		{
			return _socket != null && _socket.Connected && _state >= State.Connected;
		}
	}
}

bool IsSendable
{
	get
	{
		if (_isPaused)
			return false;

		if (_sendingMessages.Count > 0)
			return false;

		return true;
	}
}

public void SendMessage(OutgoingMessage message, bool sendingFirst = false)
{
	if (!sendingFirst && _state != State.Established)
	{
		lock (_sendingLock)
		{
			_unsentMessages.Enqueue(message);

			debug.Log("[{0}] - '{1}' message queued. state:{2}", msg.msg_type, state_);
		}
	}
	else
	{
		InternalSendMessage(message, sendingFirst);
	}
}

private void InternalSendMessage(OutgoingMessage message, bool sendingFirst)
{
	try
	{
		lock (_sendingLock)
		{
			if (sendingFirst)
				_firstSendableMessages.Add(message);
			else
				_pendingSendableMessages.Add(message);

			if (IsConnected && IsSendable)
				SendPendingMessages();
		}
	}
	catch (Exception e)
	{
		var error = new TransportError();
		error.Type = TransportError.Type.SendingFailed;
		error.Message = string.Format("Failure in InternalSendMessage: {0}", e.ToString());
		OnFailure(error);
	}
}

private void CheckPendingMessages()
{
	lock (_sendingLock)
	{
		if (_sendingMessages.Count > 0)
		{
			WireSend();
		}
		else if (IsSendable)
		{
			SendPendingMessages();
		}
	}
}

private void SendPendingMessages()
{
	try
	{
		lock (_sendingLock)
		{
			if (!IsSendable)
				return;

			if (_firstSendableMessages.Count == 0 && _pendingSendableMessages.Count == 0)
				return;

			List<OutgoingMessages> tmp = _sendingMessages;
			if (_firstSendableMessages.Count > 0)
			{
				_sendingMessages = _firstSendableMessages;
				_firstSendableMessages = tmp;
			}
			else
			{
				// 인증전에는 first 메시지가 아니면 전송하지 않는다.
				if (SessionId == 0)
					return;

				_sendingMessages = _pendingSendableMessages;
				_pendingSendableMessages = tmp;
			}

			foreach (var message in _sendingMessages)
			{
				if (!message.IsEncoded)
					message.Build(KeyChain);
				else
					message.Rebuild(KeyChain);

				// 전송시에 PackedHeader, PackedBody가 수정될수 있으므로, 여기서 재설정해줘야함.
				message.SendablePackedHeader = message.PackedHeader;
				message.SendablePackedBody = message.PackedBody;
			}

			WireSend();
		}
	}
	catch (Exception e)
	{
		var error = new TransportError();
		error.type = TransportError.Type.SendingFailed;
		error.Message = string.Format("[{0}] Failure in sendPendingMessages: {1}", str_protocol_, e.ToString());
		OnFailure(error);
	}
}

private void WireSend()
{
    try
    {
		// 어짜피 중첩되어서 처리하지는 않으니
		// 객체마다 하나씩 가지고 있다가 재사용해주는게 좋을듯.
        var list = new List<ArraySegment<byte>>();
        int length = 0;

        lock (_sendingLock)
        {
			// 아직 보내고 있다면 대기해야함.
            if (_sendIssuedLength > 0)
			{
				return;
			}

			// 보내야할 메시지들 전송 목록 만들기.
			// zero copy
            foreach (var m in _sendingMessages)
            {
				// 최소 한개는 추가된 상태에서 송신 버퍼길이를 초과한 경우에는
				// 추가하지 않는다.
				// 그러면 이 메시지는 버려지는건가? 다음턴에 되는건가?
                if (list.Count > 0 &&
					(length + m.SendablePackedHeader.Count + m.SendablePackedBody.Count) > SendBufferMax)
                {
                    break;
                }

                // Send headers unconditionally.
                if (m.SendablePackedHeader.Count > 0)
                {
                    list.Add(m.SendablePackedHeader);
                    length += m.SendablePackedHeader.Count;
                }

                // Send bodies but if the length is larger than SendBufferMax, sends it in pieces.
                if (m.SendablePackedBody.Count > 0)
                {
                    if (length + m.SendablePackedBody.Count > SendBufferMax)
                    {
                        int partialSent = SendBufferMax - length;
                        list.Add(new ArraySegment<byte>(m.SendablePackedBody.Array, m.SendablePackedBody.Offset, partialSent));
                        length += partialSent;
                        break;
                    }
                    else
                    {
                        list.Add(m.SendablePackedBody);
                        length += m.SendablePackedBody.Count;
                    }
                }
            }
        }

        lock (_socketLock)
        {
            _sendIssuedLength = length;
            sock_.BeginSend(list, SocketFlags.None, new AsyncCallback(OnSendCompleted), this);
        }
    }
    catch (Exception e)
    {
        if (e is ObjectDisposedException || e is NullReferenceException)
        {
            debug.LogDebug("[TCP] BeginSend operation has been cancelled.");
            return;
        }

        var error = new TransportError();
        error.Type = TransportError.Type.SendingFailed;
        error.Message = "[TCP] Failure in WireSend: " + e.ToString();
        OnFailure(error);
    }
}

void OnSendCompleted(IAsyncResult ar)
{
    try
    {
        int sent = 0;

        lock (_socketLock)
        {
            if (_socket == null)
				return;

            sent = _socket.EndSend(ar);
        }

		if (sent <= 0)
		{
			debug.LogDebug("OnSendCompleted: socket is closed.");
			return;
		}

		lock (_sendingLock)
		{
			while (sent > 0)
			{
				if (_sendingMessages.Count > 0)
				{
					var m = _sendingMessages[0];
					int length = m.SendablePackedHeader.Count + m.SendablePackedBody.Count;

					if (length <= sent)
					{
						if (m.SendablePackedHeader.Count == 0)
							debug.LogDebug("OnSendCompleted: Partially sent {0} bytes. 0 bytes left.", sent);

						sent -= length;
						_sendingMessages.RemoveAt(0);

						// 재전송이 필요 없는 메시지는 여기서 풀에 반납하도록 한다.
						// 재전송이 필요한 메시지는 명시적으로 연결을 끊어서 세션을 만료시키거나
						// 상대편에서 수신확인에 해당하는 ack를 받았을때만 명시적으로 풀에 반납해야함.
						if (!m.Seq.HasValue)
							m.Return();
					}
					else
					{
						int offset = sent - m.SendablePackedHeader.Count;

						// clear header
						m.SendablePackedHeader = new ArraySegment<byte>();

						//todo slice로 처리할까?
						m.SendablePackedBody = new ArraySegment<byte>(m.SendablePackedBody.Array, m.SendablePackedBody.Offset + offset, m.SendablePackedBody.Count - offset);
						debug.LogDebug("OnSendCompleted: Partially sent {0} bytes. {1} bytes left.", sent, m.SendablePackedBody.Count);
						break;
					}
				}
				else
				{
					debug.LogError($"OnSendCompleted: Sent {sent} more bytes but couldn't find the sending buffer.");
				}
			}

			if (_sendingMessages.Count > 0)
			{
				// There are still messages being sent.
				debug.LogDebug("OnSendCompleted: {0} message(s) left in the sending buffer.", _sendingMessages.Count);
			}

			// Clear send issue.
			_sendIssuedLength = 0;

			// If you still have more messages to send, ask them to send them again.
			CheckPendingMessages();
		}
    }
    catch (ObjectDisposedException)
    {
        debug.LogDebug("OnSendCompleted: BeginSend operation has been cancelled.");
    }
    catch (Exception e)
    {
        var error = new TransportError();
        error.Type = TransportError.Type.SendingFailed;
        error.Message = "OnSendCompleted: Failure=" + e.ToString();
        OnFailure(error);
    }
}

private int GetRequiredSendingBufferLength()
{
	int length = 0;

	lock (_sendingLock)
	{
		foreach (var m in _sendingMessages)
		{
			int messageLength = m.PackedHeader.Count + m.PackedBody.Count;
			if (length > 0 && (length + messageLength) > SendBufferMax)
				break;

			length += messageLength;
		}
	}

	return length;
}

private void ParseMessages()
{
    lock (_messagesLock)
    {
        while (true)
        {
            if (_nextDecodingOffset >= _receivedLength)
                break;

            int offset = _nextDecodingOffset;

            // 현재 위치에서 길이를 가져옴
            if ((offset + 2) > _receivedLength)
            {
                // 대기
                break;
            }

            var span = new Span<byte>(_receivedLength, offset, _receiveBuffer - offset);

            int length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 2));
            span = span.Slice(2);

            if (length > span.Length)
            {
                //대기
                break;
            }

            ArraySegment<byte> data = new ArraySegment<byte>(_receiveBuffer, _nextDecodingOffset, ??);
            _nextDecodingOffset += length;
        }
    }
}

void CheckReceiveBuffer(int additionalLength)
{
    int remainingSize = _receiveBuffer.Length - (_receivedLength + additionalLength);
    if (remainingSize > 0)
        return;

    int retainLength = _receivedLength - _nextDecodingOffset + additionalLength;
    int newLength = _receiveBuffer.Length;
    while (newLength <= retainLength)
        newLength += kUnitBufferSize;

    byte[] newBuffer = new byte[newLength];

    // If there are spaces that can be collected, compact it first.
    // Otherwise, increase the receiving buffer size.
    if (_nextDecodingOffset > 0)
    {
        // fit in the receive buffer boundary.
        debug.LogDebug("Compacting the receive buffer to save {0} bytes.", _nextDecodingOffset);
        Buffer.BlockCopy(_receiveBuffer, _nextDecodingOffset, newBuffer, 0, _receivedLength - _nextDecodingOffset);
        _receiveBuffer = newBuffer;
        _receivedLength -= _nextDecodingOffset;
        _nextDecodingOffset = 0;
    }
    else
    {
        debug.LogDebug("Increasing the receive buffer to {0} bytes.", newLength);
        Buffer.BlockCopy(_receiveBuffer, 0, newBuffer, 0, _receivedLength);
        _receiveBuffer = newBuffer;
    }
}
