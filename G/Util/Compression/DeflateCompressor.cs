using System;
using System.IO;
using System.IO.Compression;
using System.Buffers;

namespace G.Util.Compression
{
    public class DeflateCompressor : ICompressor
    {
        public int CompressionThreshold => 128;

        public DeflateCompressor()
        {
        }

        public int GetMaximumOutputLength(int length)
        {
            // https://github.com/madler/zlib/blob/master/compress.c
            return length + (length >> 12) + (length >> 14) + (length >> 25) + 13;
        }

        public ArraySegment<byte> Compress(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count == 0)
            {
                // Empty
                return new ArraySegment<byte>();
            }

            using (var output = new MemoryStream())
            {
                Stream compressor = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true);
                compressor.Write(src.Array, src.Offset, src.Count);
                compressor.Flush();

                // If the compressed size is larger than the original, it is considered uncompressed.
                if (output.Length > src.Count)
                {
                    // Give up
                    return new ArraySegment<byte>();
                }

                //return output.ToArray();
                return new ArraySegment<byte>(output.GetBuffer(), 0, (int)output.Position);
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
            using (var input = new MemoryStream(src.Array, src.Offset, src.Count))
            {
                Stream decompressor = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
                int decompressedLength = decompressor.Read(decompressed, 0, expectedLength);
                if (decompressedLength != expectedLength)
                {
                    // Failure
                    return new ArraySegment<byte>();
                }

                return new ArraySegment<byte>(decompressed, 0, decompressedLength);
            }
        }

        public ArraySegment<byte> CompressWithArrayPool(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count == 0)
            {
                // Empty
                return new ArraySegment<byte>();
            }

            byte[] compressed = ArrayPool<byte>.Shared.Rent(GetMaximumOutputLength(src.Count));
            try
            {
                using (var output = new MemoryStream(compressed))
                {
                    Stream compressor = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true);
                    compressor.Write(src.Array, src.Offset, src.Count);
                    compressor.Flush();

                    // If the compressed size is larger than the original, it is considered uncompressed.
                    if (output.Length > src.Count)
                    {
                        // Give up
                        return new ArraySegment<byte>();
                    }

                    var ret = new ArraySegment<byte>(output.GetBuffer(), 0, (int)output.Position);
                    compressed = null; // Take ownership
                    return ret;
                }
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
                using (var input = new MemoryStream(src.Array, src.Offset, src.Count))
                {
                    Stream decompressor = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
                    int decompressedLength = decompressor.Read(decompressed, 0, expectedLength);
                    if (decompressedLength != expectedLength)
                    {
                        // Failure
                        ArrayPool<byte>.Shared.Return(decompressed);
                        return new ArraySegment<byte>();
                    }

                    var ret = new ArraySegment<byte>(decompressed, 0, decompressedLength);
                    decompressed = null; // Take ownership
                    return ret;
                }
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
