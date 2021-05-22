namespace G.Util.Compression
{
    /// <summary>
    /// CompressionType
    /// </summary>
    public enum CompressionType : byte
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,

#if SUPPORTS_LZ4_COMPRESSION
        /// <summary>
        /// LZ4
        /// </summary>
        LZ4 = 1,
#endif

        /// <summary>
        /// Deflate
        /// </summary>
        Deflate = 2,

        /// <summary>
        /// GZip
        /// </summary>
        GZip = 3,
    }
}
