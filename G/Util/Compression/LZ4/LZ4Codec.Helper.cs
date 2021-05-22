#if SUPPORTS_LZ4_COMPRESSION

// The original source code was taken from the repository below.
// https://github.com/neuecc/MessagePack-CSharp/tree/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/LZ4

using System;

namespace G.Util.Compression.LZ4
{
    public partial class LZ4Codec
    {
#if UNITY_2018_3_OR_NEWER

        // use 'Safe' code for Unity because in IL2CPP gots strange behaviour.

        public static int Encode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset, int outputLength)
        {
            if (IntPtr.Size == 4)
            {
                return LZ4Codec.Encode32Safe(input, inputOffset, inputLength, output, outputOffset, outputLength);
            }
            else
            {
                return LZ4Codec.Encode64Safe(input, inputOffset, inputLength, output, outputOffset, outputLength);
            }
        }

        public static int Decode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset, int outputLength)
        {
            if (IntPtr.Size == 4)
            {
                return LZ4Codec.Decode32Safe(input, inputOffset, inputLength, output, outputOffset, outputLength);
            }
            else
            {
                return LZ4Codec.Decode64Safe(input, inputOffset, inputLength, output, outputOffset, outputLength);
            }
        }

#endif

        internal static class HashTablePool
        {
            [ThreadStatic]
            private static ushort[] _ushortPool;

            [ThreadStatic]
            private static uint[] _uintPool;

            [ThreadStatic]
            private static int[] _intPool;

            public static ushort[] GetUShortHashTablePool()
            {
                if (_ushortPool == null)
                {
                    _ushortPool = new ushort[HASH64K_TABLESIZE];
                }
                else
                {
                    Array.Clear(_ushortPool, 0, _ushortPool.Length);
                }

                return _ushortPool;
            }

            public static uint[] GetUIntHashTablePool()
            {
                if (_uintPool == null)
                {
                    _uintPool = new uint[HASH_TABLESIZE];
                }
                else
                {
                    Array.Clear(_uintPool, 0, _uintPool.Length);
                }

                return _uintPool;
            }

            public static int[] GetIntHashTablePool()
            {
                if (_intPool == null)
                {
                    _intPool = new int[HASH_TABLESIZE];
                }
                else
                {
                    Array.Clear(_intPool, 0, _intPool.Length);
                }

                return _intPool;
            }
        }
    }
}

#endif
