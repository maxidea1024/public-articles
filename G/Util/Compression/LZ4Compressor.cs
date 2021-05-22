#if SUPPORTS_LZ4_COMPRESSION

using System;
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
            byte[] compressed = new byte[GetMaxiumOutputLength(src.Count)];

            int compressedLength = LZ4Codec.Encode(src, compressed);

            // If the compressed size is larger than the original, it is considered uncompressed.
            if (compressedLength > src.Count)
            {
                // Give up
                compressedLength = 0;
            }

            return new ArraySegment<byte>(compressed, 0, compressedLength);
        }

        public ArraySegment<byte> Decompress(ArraySegment<byte> src, int expectedLength)
        {
            byte[] decompressed = new byte[expectedLength];

            int decompressedLength = LZ4Codec.Decode(src, decompressed);
            if (decompressedLength != expectedLength)
            {
                // Failure
                decompressedLength = 0;
            }

            return new ArraySegment<byte>(decompressed, 0, decompressedLength);
        }
    }
}

#endif
