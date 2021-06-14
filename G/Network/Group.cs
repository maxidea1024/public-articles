/*
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using G.Util;

namespace G.Network
{
    public class Group
    {
        private static NLog.Logger log =  NLog.LogManager.GetCurrentClassLogger();

        protected AsyncLock semaphoreLock = new AsyncLock(TimeSpan.FromSeconds(20));

        public int RemoteMax { get; private set; }
        protected HashSet<TcpRemote> remoteSet;
        protected List<TcpRemote> remoteList;

        public int RemoteCount { get { return remoteList.Count; } }

        private KeyIndex keyIndex;
        private XXTea xxtea = new XXTea();
        public uint[] KeyArray { get; protected set; }
        public string Key { get; protected set; }

        public Group(int remoteMax, KeyIndex keyIndex)
        {
            RemoteMax = remoteMax;
            this.keyIndex = keyIndex;
            KeyArray = xxtea.Key;
            Key = ConvertEx.ToBase62(KeyArray);

            remoteSet = new HashSet<TcpRemote>();
            remoteList = new List<TcpRemote>(remoteMax);
        }

        //public async Task<_Semaphore> LockAsync()
        //{
        //  return await semaphoreLock.LockAsync();
        //}

        public virtual async Task ResetAsync()
        {
            using (await semaphoreLock.LockAsync())
            {
                remoteSet.Clear();
                remoteList.Clear();
            }
        }

        public async Task ResetKey()
        {
            using (await semaphoreLock.LockAsync())
            {
                xxtea.SetKey();
                KeyArray = xxtea.Key;
                Key = ConvertEx.ToBase62(KeyArray);
            }
        }

        public async Task<TcpRemote[]> GetRemotesAsync()
        {
            using (await semaphoreLock.LockAsync())
            {
                return remoteList.ToArray();
            }
        }

        public async Task<int> EnterAsync(GroupMembership gm)
        {
            using (await semaphoreLock.LockAsync())
            {
                if (gm == null) return -1;
                if (RemoteCount >= RemoteMax) return -2;

                TcpRemote remote = gm.Remote;

                if (await OnEnteringAsync(remote) == false) return -3;
                if (gm.SetGroup(this) == false) return -4;

                if (remoteSet.Add(remote) == false)
                {
                    gm.ResetGroup(this);
                    log.Error("Group.Enter : Impossible Error");
                    return -5;
                }

                remote.SetKey(keyIndex, xxtea.Key);

                remoteList.Add(remote);

                await OnEnteredAsync(remote);

                return RemoteCount;
            }
        }

        public async Task<int> LeaveAsync(GroupMembership gm)
        {
            using (await semaphoreLock.LockAsync())
            {
                if (gm == null) return -1;

                TcpRemote remote = gm.Remote;

                if (await OnLeavingAsync(remote) == false) return -2;
                if (gm.ResetGroup(this) == false) return -3;

                if (remoteSet.Remove(remote) == false)
                {
                    log.Error("Group.Leave : Impossible Error");
                    return -4;
                }
                remoteList.Remove(remote);

                await OnLeavedAsync(remote);

                return RemoteCount;
            }
        }

        public async Task<TcpRemote> GetAtAsync(int index)
        {
            using (await semaphoreLock.LockAsync())
            {
                try
                {
                    return remoteList[index];
                }
                catch
                {
                    return null;
                }
            }
        }

        public async Task<bool> ContainsAsync(TcpRemote remote)
        {
            using (await semaphoreLock.LockAsync())
            {
                if (remote == null) return false;
                return remoteSet.Contains(remote);
            }
        }

        public async Task<TcpRemote> FindAsync(long remoteId)
        {
            using (await semaphoreLock.LockAsync())
            {
                foreach (TcpRemote r in remoteList)
                {
                    if (r.Id == remoteId) return r;
                }
                return null;
            }
        }

        public async Task SendAsync(ReadOnlyMemory<byte> memory, bool useRemoteKey = false)
        {
            try
            {
                using (await semaphoreLock.LockAsync())
                {
                    var remotes = remoteList.ToArray();

                    if (useRemoteKey)
                    {
                        foreach (var r in remotes)
                        {
                            r.Send(memory, KeyIndex.Remote, 0);
                        }
                    }
                    else
                    {
                        if (memory.IsEmpty)
                        {
                            log.Error("Encryption is failed");
                            return;
                        }

                        foreach (var r in remotes)
                        {
                            r.Send(memory, keyIndex, 0, true);
                        }
                    }
                }
            }
            finally
            {
                if (bm.Buffer != null) ArrayPool<byte>.Shared.Return(bm.Buffer);
            }
        }

        public async Task SendToAsync(long toRemoteId, ReadOnlyMemory<byte> memory, bool useRemoteKey = false)
        {
            (byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);

            try
            {
                if (!useRemoteKey)
                {
                    bm = xxtea.EncryptUsingArrayPool(memory);
                    if (bm.Buffer != null)
                    {
                        memory = bm.Memory;
                    }
                }

                using (await semaphoreLock.LockAsync())
                {
                    var remotes = remoteList.ToArray();

                    if (useRemoteKey)
                    {
                        foreach (var r in remotes)
                        {
                            if (r.Id == toRemoteId)
                            {
                                r.Send(memory, KeyIndex.Remote, 0, true);
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (memory.IsEmpty)
                        {
                            log.Error("Encryption is failed");
                            return;
                        }

                        foreach (var r in remotes)
                        {
                            if (r.Id == toRemoteId)
                            {
                                r.Send(memory, keyIndex, 0, true);
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (bm.Buffer != null) ArrayPool<byte>.Shared.Return(bm.Buffer);
            }
        }

        public async Task SendToOthersAsync(TcpRemote me, ReadOnlyMemory<byte> memory, bool useRemoteKey = false)
        {
            (byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);

            try
            {
                if (!useRemoteKey)
                {
                    bm = xxtea.EncryptUsingArrayPool(memory);
                    if (bm.Buffer != null)
                    {
                        memory = bm.Memory;
                    }
                }

                using (await semaphoreLock.LockAsync())
                {
                    var remotes = remoteList.ToArray();

                    if (useRemoteKey)
                    {
                        foreach (var r in remotes)
                        {
                            if (r != me)
                            {
                                r.Send(memory, KeyIndex.Remote, 0, true);
                            }
                        }
                    }
                    else
                    {
                        if (memory.IsEmpty)
                        {
                            log.Error("Encryption is failed");
                            return;
                        }

                        foreach (var r in remotes)
                        {
                            if (r != me)
                            {
                                r.Send(memory, keyIndex, 0, true);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (bm.Buffer != null) ArrayPool<byte>.Shared.Return(bm.Buffer);
            }
        }

#pragma warning disable 1998
        protected virtual async Task<bool> OnEnteringAsync(TcpRemote remote)
        {
            return true;
        }

        protected virtual async Task OnEnteredAsync(TcpRemote remote)
        {
        }

        protected virtual async Task<bool> OnLeavingAsync(TcpRemote remote)
        {
            return true;
        }

        protected virtual async Task OnLeavedAsync(TcpRemote remote)
        {
        }
        #pragma warning restore 1998
    }
}
*/
