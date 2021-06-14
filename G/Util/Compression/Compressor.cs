using System;

#if SUPPORTS_LZ4_COMPRESSION
using G.Util.Compression.LZ4;
#endif

namespace G.Util.Compression
{
    public class Compressor
    {
        /// <summary>
        /// Compression threshold.
        ///
        /// If the length of the data to be compressed is less than this value, compression is not performed.
        /// </summary>
        public static int GetCompressionThreshold(CompressionType compressionType)
        {
            var compressor = GetCompressor(compressionType);
            return compressor.CompressionThreshold;
        }

        /// <summary>
        /// Maximum compression length possible based on the given data length
        /// </summary>
        public static int GetMaximumOutputLength(CompressionType compressionType, int length)
        {
            var compressor = GetCompressor(compressionType);
            return compressor.GetMaximumOutputLength(length);
        }

        /// <summary>
        /// Compress the data.
        /// </summary>
        public static ArraySegment<byte> Compress(CompressionType compressionType, ArraySegment<byte> src)
        {
            var compressor = GetCompressor(compressionType);
            return compressor.Compress(src);
        }

        /// <summary>
        /// Decompress the compressed data.
        /// </summary>
        public static ArraySegment<byte> Decompress(CompressionType compressionType, ArraySegment<byte> src, int expectedLength)
        {
            var compressor = GetCompressor(compressionType);
            return compressor.Decompress(src, expectedLength);
        }


        /// <summary>
        /// Compress the data with ArrayPool<byte>.
        /// </summary>
        public static ArraySegment<byte> CompressWithArrayPool(CompressionType compressionType, ArraySegment<byte> src)
        {
            var compressor = GetCompressor(compressionType);
            return compressor.CompressWithArrayPool(src);
        }

        /// <summary>
        /// Decompress the compressed data with ArrayPool<byte>.
        /// </summary>
        public static ArraySegment<byte> DecompressWithArrayPool(CompressionType compressionType, ArraySegment<byte> src, int expectedLength)
        {
            var compressor = GetCompressor(compressionType);
            return compressor.DecompressWithArrayPool(src, expectedLength);
        }


        #region Private

        private static ICompressor GetCompressor(CompressionType compressionType)
        {
            switch (compressionType)
            {
#if SUPPORTS_LZ4_COMPRESSION
                case CompressionType.LZ4: return _lz4Compressor;
#endif
                case CompressionType.Deflate: return _deflateCompressor;
                case CompressionType.GZip: return _gzipCompressor;
            }

            throw new Exception($"Unsupported compressor {compressionType}");
        }


        private static readonly DeflateCompressor _deflateCompressor = new DeflateCompressor();
        private static readonly GZipCompressor _gzipCompressor = new GZipCompressor();
#if SUPPORTS_LZ4_COMPRESSION
        private static readonly LZ4Compressor _lz4Compressor = new LZ4Compressor();
#endif

        #endregion
    }
}
