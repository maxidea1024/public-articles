readonly ConcurrentDictionary<LinkId, PeerGroup> _groups = new ConcurrentDictionary<LinkId, PeerGroup>();

private PeerGroup GetOrCreateGroup(LinkId groupId)
{
    lock (_mainLock)
    {
        var newGroup = new PeerGroup
        {
            GroupId = groupId
        };
        _groups.Add(groupId, newGroup);
        return newGroup;
    }
}

//이벤트처리하는 동안 메인락은 잡지 않는가?
internal void HandleGroupMemberJoinedEvent(LinkId groupId, LinkId memberId, int memberCount, byte[] data)
{
    var group = GetOrCreateGroup(groupId);

    if (memberId != LinkId.Server)
    {
        var member = GetPeer(memberId);
        if (peer= = null)
        {
        }
        else if (peer.IsGarbage)
        {
        }

        peer.JoinedGroups.Add(group.GroupId);
        group.Members.Add(memberId);
    }
    else
    {
        //todo 서버를 멤버로 참여시킬 수 있게하자.
    }

    EnqueueLocalEvent(new MemberLocalEvent
    {
        Type = LocalEventType.GroupMemberJoined,
        GroupID = groupID,
        MemberID = memberID,
        MemberCount = group.Members.Count,
        UserData = data
    });
}
