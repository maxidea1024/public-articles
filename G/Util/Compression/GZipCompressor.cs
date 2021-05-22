using System;
using System.IO;
using System.IO.Compression;

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

                return new ArraySegment<byte>(output.ToArray());
            }
        }

        public ArraySegment<byte> Decompress(ArraySegment<byte> src, int expectedLength)
        {
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
        
        
        
        //todo
        /*
        public Memory<byte> Compress(ReadOnlyMemory<byte> src)
        {
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

                return new ArraySegment<byte>(output.ToArray());
            }
        }

        public Memory<byte> Decompress(ReadOnlyMemory<byte> src, int expectedLength)
        {
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
        */
    }
}
