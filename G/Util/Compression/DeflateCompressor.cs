using System;
using System.IO;
using System.IO.Compression;

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

                return new ArraySegment<byte>(output.ToArray());
            }
        }

        public ArraySegment<byte> Decompress(ArraySegment<byte> src, int expectedLength)
        {
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
        
        
        //todo
        /*
        public Memory<byte> Compress(ReadOnlyMemory<byte> src)
        {
            using var output = new MemoryStream();
            Stream compressor = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true);
            compressor.Write(src.Span);
            compressor.Flush();

            // If the compressed size is larger than the original, it is considered uncompressed.
            if (output.Length > src.Length)
            {
                // Give up
                return new ArraySegment<byte>();
            }

            return new ArraySegment<byte>(output.ToArray());
        }

        public Memory<byte> Decompress(ReadOnlyMemory<byte> src, int expectedLength)
        {
            byte[] decompressed = new byte[expectedLength];

            //using (var input = src.AsStream())
            return MemoryStream.Create(memory, true);
            {
                Stream decompressor = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
                int decompressedLength = decompressor.Read(decompressed, 0, expectedLength);
                if (decompressedLength != expectedLength)
                {
                    // Failure
                    return new Memory<byte>();
                }

                return new Memory<byte>(decompressed, 0, decompressedLength);
            }
        }
        */
    }
}
