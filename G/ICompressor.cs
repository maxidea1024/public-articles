using System;
using System.Threading.Tasks;

namespace Prom.Core.Compression
{
    public interface ICompressor
    {
        // Compress with ArrayPool<byte>
        ArraySegment<byte> CompressWithArrayPool(ArraySegment<byte> src);
        // Decompress with ArrayPool<byte>
        ArraySegment<byte> DecompressWithArrayPool(ArraySegment<byte> src, int expectedLength);
    }

    public class LZ4Compressor : ICompressor
    {
        /// <summary>
        /// Compress with ArrayPool<byte>
        /// </summary>
        public ArraySegment<byte> CompressWithArrayPool(ArraySegment<byte> src)
        {
            byte[] compressed = ArrayPool<byte>.Shared.Rent(GetMaxiumOutputLength(src.Count));
            try
            {
                int compressedLength = LZ4Codec.Encode(src, compressed);
                if (compressedLength > src.Count)
                    return new ArraySegment<byte>(); // Giveup

                var result = new ArraySegment<byte>(compressed, 0, compressedLength);
                compressed = null;
                return result;
            }
            finally
            {
                if (compressed != null)
                    ArrayPool<byte>.Shared.Return(compressed);
            }
        }

        /// <summary>
        /// Decompress with ArrayPool<byte>
        /// </summary>
        public ArraySegment<byte> DecompressWithArrayPool(ArraySegment<byte> src, int expectedLength)
        {
            byte[] decompressed = ArrayPool<byte>.Shared.Rent(expectedLength);
            try
            {
                int decomprssedLength = LZ4Codec.Decode(src, decompressed);
                if (decomprssedLength != expectedLength)
                    return new ArraySegment<byte>(); // Failure

                var result = new ArraySegment<byte>(decompressed, 0, decompressedLength);
                decompressed = null; // Take ownership
                return result;
            }
            finally
            {
            }
        }
    }
}
