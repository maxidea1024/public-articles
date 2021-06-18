namespace Lane.Realtime.Server.Internal
{
    internal class Group
    {
        public LinkId GroupId { get; set; }
        public object LinkTag { get; set; }
        public RtGroupOptions Options { get; private set; }
        public List<LinkId> Members { get; private set; }
        public bool IsEmpty => Members.Count == 0;
        public long CreatedTime { get; private set; }
        public long? EmptyTime { get; private set; }

        private readonly RtServer _server;

        public RtGroup(RtServer server, LinkId groupId, RtGroupOptions options)
        {
            _server = PreValidations.CheckNotNull(server);

            GroupId = groupId;
            Members = new List<LinkId>();
            Options = options;
            CreatedTime = _server.HeartbeatTime;
            EmptyTime = _server.HeartbeatTime;
        }

        public bool IsMember(LinkId memberId)
        {
            return Members.Contains(memberId);
        }

        public bool Join(RemoteClient client)
        {
            PreValidations.CheckNotNull(client);

            if (!client.JoinedGroups.Contains(GroupId))
            {
                client.JoinedGroups.Add(GroupId);
                Members.Add(link.LinkId);
                EmptyTime = null;
                return true;
            }

            return false;
        }

        public bool Leave(RemoteClient client)
        {
            PreValidations.CheckNotNull(client);

            if (client.JoinedGroups.Contains(GroupId))
            {
                client.JoinedGroups.Remove(GroupId);
                Members.Remove(client.LinkId);

                // 비워진 시각 기록하기.
                if (Members.Count == 0)
                {
                    EmptyTime = _server.HeartbeatTime;
                }

                return true;
            }

            return false;
        }
    }
}
