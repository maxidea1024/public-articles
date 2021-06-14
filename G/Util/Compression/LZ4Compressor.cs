#if SUPPORTS_LZ4_COMPRESSION

using System;
using System.Buffers;
using G.Util.Compression.LZ4;

namespace G.Util.Compression
{
    public class LZ4Compressor : ICompressor
    {
        public int CompressionThreshold => 128;

        public LZ4Compressor()
        {
        }

        public int GetMaximumOutputLength(int length)
        {
            return LZ4Codec.MaximumOutputLength(length);
        }

        public ArraySegment<byte> Compress(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count == 0)
            {
                // Empty
                return new ArraySegment<byte>();
            }

            byte[] compressed = new byte[GetMaxiumOutputLength(src.Count)];
            try
            {
                int compressedLength = LZ4Codec.Encode(src, compressed);

                // If the compressed size is larger than the original, it is considered uncompressed.
                if (compressedLength > src.Count)
                {
                    // Give up
                    compressedLength = 0;
                }

                return new ArraySegment<byte>(compressed, 0, compressedLength);
            }
            catch (Exception)
            {
                return new ArraySegment<byte>();
            }
        }

        public ArraySegment<byte> Decompress(ArraySegment<byte> src, int expectedLength)
        {
            if (src.Array == null || src.Count == 0 || expectedLength <= 0)
            {
                // Empty
                return new ArraySegment<byte>();
            }

            byte[] decompressed = new byte[expectedLength];
            try
            {
                int decompressedLength = LZ4Codec.Decode(src, decompressed);
                if (decompressedLength != expectedLength)
                {
                    // Failure
                    decompressedLength = 0;
                }

                return new ArraySegment<byte>(decompressed, 0, decompressedLength);
            }
            catch (Exception)
            {
                return new ArraySegment<byte>();
            }
        }

        public ArraySegment<byte> CompressWithArrayPool(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count == 0)
            {
                // Empty
                return new ArraySegment<byte>();
            }

            byte[] compressed = ArrayPool<byte>.Shared.Rent(GetMaxiumOutputLength(src.Count));
            try
            {
                int compressedLength = LZ4Codec.Encode(src, compressed);

                // If the compressed size is larger than the original, it is considered uncompressed.
                if (compressedLength > src.Count)
                {
                    // Give up
                    return new ArraySegment<byte>();
                }

                var ret = new ArraySegment<byte>(compressed, 0, compressedLength);
                compressed = null; // Take ownership
                return ret;
            }
            finally
            {
                if (compressed != null)
                {
                    ArrayPool<byte>.Shared.Return(compressed);
                }
            }
        }

        public ArraySegment<byte> DecompressWithArrayPool(ArraySegment<byte> src, int expectedLength)
        {
            if (src.Array == null || src.Count == 0 || expectedLength <= 0)
            {
                // Empty
                return new ArraySegment<byte>();
            }

            byte[] decompressed = ArrayPool<byte>.Shared.Rent(expectedLength);
            try
            {
                int decompressedLength = LZ4Codec.Decode(src, decompressed);
                if (decompressedLength != expectedLength)
                {
                    // Failure
                    return new ArraySegment<byte>();
                }

                var ret = new ArraySegment<byte>(decompressed, 0, decompressedLength);
                decompressed = null; // Take ownership
                return ret;
            }
            finally
            {
                if (decompressed != null)
                {
                    ArrayPool<byte>.Shared.Return(decompressed);
                }
            }
        }
    }
}

#endif
