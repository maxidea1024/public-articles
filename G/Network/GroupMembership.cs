/*
namespace G.Network
{
    public class GroupMembership
    {
        public TcpRemote Remote { get; private set; }
        public Group Group { get; private set; }

        public GroupMembership(TcpRemote remote)
        {
            Remote = remote;
            Group = null;
        }

        public bool SetGroup(Group g)
        {
            lock (this)
            {
                if (Group != null) return false;
                if (g == null) return false;
                Group = g;
                return true;
            }
        }

        public bool ResetGroup(Group g)
        {
            lock (this)
            {
                if (Group == null) return false;
                if (g == null) return false;
                if (g != Group) return false;
                Group = null;
                return true;
            }
        }
    }
}
*/
