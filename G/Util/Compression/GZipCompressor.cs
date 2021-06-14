using System;
using System.IO;
using System.IO.Compression;
using System.Buffers;

namespace G.Util.Compression
{
    public class GZipCompressor : ICompressor
    {
        public int CompressionThreshold => 128;

        public GZipCompressor()
        {
        }

        public int GetMaximumOutputLength(int length)
        {
            // https://github.com/richgel999/miniz/blob/master/miniz.c
            return Math.Max(128 + (length * 110) / 100, 128 + length + ((length / (31 * 1024)) + 1) * 5);
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
                Stream compressor = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
                compressor.Write(src.Array, src.Offset, src.Count);
                compressor.Flush();

                // If the compressed size is larger than the original, it is considered uncompressed.
                if (output.Length > src.Count)
                {
                    // Give up
                    return new ArraySegment<byte>();
                }

                //return output.ToArray();
                return new ArraySegment<byte>(output.GetBuffer(), 0, (int) output.Position);
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
                Stream decompressor = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
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
                    Stream compressor = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
                    compressor.Write(src.Array, src.Offset, src.Count);
                    compressor.Flush();

                    // If the compressed size is larger than the original, it is considered uncompressed.
                    if (output.Length > src.Count)
                    {
                        // Give up
                        return new ArraySegment<byte>();
                    }

                    var ret = new ArraySegment<byte>(output.GetBuffer(), 0, (int) output.Position);
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
                    Stream decompressor = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
                    int decompressedLength = decompressor.Read(decompressed, 0, expectedLength);
                    if (decompressedLength != expectedLength)
                    {
                        // Failure
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
