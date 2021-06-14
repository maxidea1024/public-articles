using System;
using System.Buffers;

namespace G.Util
{
    public class KeyChain
    {
        private XXTea[] keyChain;

        public KeyChain()
        {
            keyChain = new XXTea[(int) KeyIndex.Max];
        }

        public KeyChain Clone()
        {
            var result = new KeyChain();
            for (int i = 0; i < (int) KeyIndex.Max; i++)
            {
                result.keyChain[i] = keyChain[i];
            }
            return result;
        }
        
        public XXTea Get(KeyIndex index)
        {
            if (index >= KeyIndex.Max || index < 0)
                return null;
            return keyChain[(int) index];
        }

        public uint[] GetKey(KeyIndex index)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return null;
            return xxtea.Key;
        }

        public void Set(KeyIndex index, XXTea xxtea)
        {
            keyChain[(int) index] = xxtea;
        }

        public void Set(KeyIndex index)
        {
            Set(index, new XXTea());
        }

        public void Set(KeyIndex index, uint[] key)
        {
            Set(index, new XXTea(key));
        }

        public void Set(KeyIndex index, string base62Key)
        {
            Set(index, new XXTea(base62Key));
        }

        public void Reset(KeyIndex index)
        {
            Set(index, default(XXTea));
        }

        public void Reset()
        {
            for (int i = 0; i < keyChain.Length; i++)
            {
                keyChain[i] = default(XXTea);
            }
        }

        public byte[] Encrypt(KeyIndex index, byte[] data, int offset, int count)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return null;
            return xxtea.Encrypt(data, offset, count);
        }

        public byte[] Encrypt(KeyIndex index, ReadOnlyMemory<byte> memory)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return null;
            return xxtea.Encrypt(memory);
        }

        public (byte[] Buffer, Memory<byte> Memory) EncryptUsingArrayPool(KeyIndex index, ReadOnlyMemory<byte> memory)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return (null, Memory<byte>.Empty);
            return xxtea.EncryptUsingArrayPool(memory);
        }

        public byte[] Decrypt(KeyIndex index, byte[] data, int offset, int count)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return null;
            return xxtea.Decrypt(data, offset, count);
        }

        public byte[] Decrypt(KeyIndex index, ReadOnlyMemory<byte> memory)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return null;
            return xxtea.Decrypt(memory);
        }

        public (byte[] Buffer, Memory<byte> Memory) DecryptUsingArrayPool(KeyIndex index, ReadOnlyMemory<byte> memory)
        {
            XXTea xxtea = Get(index);
            if (xxtea == null) return (null, Memory<byte>.Empty);
            return xxtea.DecryptUsingArrayPool(memory);
        }
    }
}
