
// 안전한 타이밍에 파괴를 수행함.
// 여러분 불리면 안됨.
private void UnsafeDestroyClient(RemoteClient client)
{
    // 풀링을 할수도 있지 않을까?
    // 유저 객체 풀링은 어떤식으로 접근해야할까?
    // 쓰는 사람이 알아서 해야하나?

    // 전송큐에서 제거(모아 보내기를 하는 경우에 해당)
    lock (_tcpSendRequestQueueLock)
        client.UnlinkFromSendRequestQueue();

    // 소켓 핸들만 닫아줌. (펜딩된 IO를 중지하기 위함)
    client.Transport.CloseSocketHandleOnly();

    // 후보 연결일 경우, 후보 목록에서 제거.
    if (client.LinkId == LinkId.None)
    {
        _candidates.Remove(client.LinkId);
        return;
    }

    // 참여했던 그룹에서 내보내기.
    
}

// async-await로 처리할 수 있으려나?
private void PurgeTooLongUnmaturedCandidates()
{
    List<RemoteClient> list = null;
    lock (_candidatesLock)
    {
        if (_candidates.Count == 0)
            return;
        
        var serverTime = HeartbeatTime;
        foreach (var pair in _candidates)
        {

        }
    }

    if (list == null)
        return;


}
