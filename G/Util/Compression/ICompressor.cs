using System;

namespace G.Util.Compression
{
    /// <summary>
    /// Base compressor interface
    /// </summary>
    public interface ICompressor
    {
        /// <summary>
        /// Compression threshold.
        ///
        /// If the length of the data to be compressed is less than this value, compression is not performed.
        /// </summary>
        int CompressionThreshold { get; }

        /// <summary>
        /// Maximum compression length possible based on the given data length
        /// </summary>
        int GetMaximumOutputLength(int length);

        /// <summary>
        /// Compress the data.
        /// </summary>
        ArraySegment<byte> Compress(ArraySegment<byte> src);

        /// <summary>
        /// Decompress the compressed data.
        /// </summary>
        ArraySegment<byte> Decompress(ArraySegment<byte> src, int expectedLength);

        //todo
        /*
        Memory<byte> Compress(ReadOnlyMemory<byte> src);
        Memory<byte> Decompress(ReadOnlyMemory<byte> src, int expectedLength);
        */
    }
}
