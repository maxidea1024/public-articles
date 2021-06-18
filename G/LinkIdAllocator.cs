
namespace Lane.Realtime.Server.Internal
{
    internal interface ILinkIdAllocator
    {
        LinkId Allocate(long time, LinkId assignedId = LinkId.None);

        void Free(LinkId id, long time);

        //todo 이함수가 필요할까?
        int GetRecycledCount(LinkId id);
    }

    internal class LinkIdAllocatorHelper
    {
        //todo interlocked으로 처리해야할까?
        protected static LinkId AdvanceId(ref LinkId id)
        {
            id = (LinkId)((uint)id + 1);

            if (id == LinkId.None)
            {
                id = (LinkID)((uint)LinkID.Last + 1);
            }

            return id;
        }
    }

    // Round-robin link-id allocator.
    internal class RoundRobinLinkIdAllocator : LinkIdAllocatorHelper, ILinkIdAllocator
    {
        private LinkId _nextId = LinkId.Last;

        public LinkId Allocate(long time, LinkId assignedId = LinkId.None)
        {
            return AdvanceId(ref _nextId);
        }

        public void Free(LinkId id, long time)
        {
            // Pass
        }

        public int GetRecycledCount(LinkId id)
        {
            return 0;
        }
    }

    internal class PooledLinkIdAllocator : LinkIdAllocatorHelper, ILinkIdAllocator
    {
        class Node
        {
            public LinkId LinkId;
            public long FreedTime;
            public int RecycledCount;
        }

        private readonly long _issuedValidTime;
        private LinkId _lastIssuedId = LinkId.Last;
        private readonly Dictionary<LinkId, Node> _freeNodes = new Dictionary<LinkId, Node>();
        private readonly Queue<LinkId> _freeList = new Queue<LinkId>();

        public PooledLinkIdAllocator(long issueValidTime)
        {
            _issuedValidTime = issuedValidTime;
            _lastIssuedId = LinkId.Last;
        }

        public LinkId Allocate(long time, LinkId assignedId = LinkId.None)
        {
            if (_freeList.Count == 0)
            {
                return AllocateNew();
            }

            var freeId = _freeList.Peek();
            if (!_freeList.TryGetValue(freeId, out Node freeNode))
            {
                _freeList.Dequeue();
                return AllocateNew();
            }

            // 안전을 위해서 일정시간내에서는 재사용하지 않음.
            if ((time - freeNode.FreedTime) > _issuedValidTime)
            {
                _freeList.Dequeue();

                freeNode.FreedTime = 0;
                freeNode.RecycledCount++;
                return freeId;
            }
        }

        public void Free(LinkId id, long time)
        {
            _idSlots[id] = false;
        }

        public int GetRecycledCount(LinkId id)
        {
            return 0;
        }
    }

    internal class PreAssignedLinkIdAllocator : LinkIdAllocatorHelper, ILinkIdAllocator
    {
        private LinkId _newId = LinkId.Last;

        private readonly Dictionay<LinkId, bool> _usedIdSlots = new Dictionary<LinkId, bool>();

        public LinkId Allocate(long time, LinkId assignedId = LinkId.None)
        {
            if (assignedId <= LinkId.Last)
            {
                return AllocateNew();
            }

            if (_idSlots.TryGetValue(assignedId, out bool isAllocated))
            {
                if (isAllocated)
                {
                    return LinkId.None;
                }
                else
                {
                    _idSlots[assignedId] = true;
                    return assignedId;
                }
            }
            else
            {
                _idSlots.Add(assignedId, true);
                return assignedId;
            }
        }

        private LinkId AllocateNew()
        {
            while (true)
            {
                AdvanceId(ref _newId);

                if (_idSlots.TryGetValue(_newId, out bool isAllocated))
                {
                    if (!isAllocated)
                    {
                        _idSlots[_newId] = true;
                        break;
                    }
                }
                else
                {
                    _idSlots.Add(_newId, true);
                    break;
                }
            }

            return _newId;
        }

        public void Free(LinkId id, long time)
        {
            _idSlots[id] = false;
        }

        public int GetRecycledCount(LinkId id)
        {
            return 0;
        }
    }
}
