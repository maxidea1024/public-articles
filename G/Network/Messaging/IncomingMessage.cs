using System;
using System.Buffers;
using System.Buffers.Binary;
using G.Util;
using G.Util.Compression;

namespace G.Network.Messaging
{
    /// <summary>
    /// Message object for receiving.
    /// </summary>
    public class IncomingMessage : IRecycleable<IncomingMessage>
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();


        /// <summary>Message Type</summary>
        public MessageType MessageType { get; set; }

        /// <summary>Optional message seq number</summary>
        public uint? Seq { get; set; }

        /// <summary>Optional message ack number</summary>
        public uint? Ack { get; set; }

        /// <summary>Optional session id(0=invalid)</summary>
        public long SessionId { get; set; }

        public long UserSuid { get; set; }

        /// <summary>Encryption key index</summary>
        public KeyIndex KeyIndex { get; set; }

        /// <summary>Remote encryption key(must be 16 bytes)</summary>
        public KeyIndex RemoteEncryptionKeyIndex { get; set; }
        public uint[] RemoteEncryptionKey { get; private set; }

        /// <summary>Compression type</summary>
        public CompressionType CompressionType { get; set; }

        /// <summary>Result code</summary>
        public NetworkResult ResultCode { get; set; }

        /// <summary>Deserialized message(Message body)</summary>
        public ReadOnlyMemory<byte> Body { get; set; }


        // internal pooled buffers
        private byte[] _decryptedBuffer;
        // Allocate once and reuse for efficiency.
        private uint[] _remoteEncryptionKeyBuffer = null;


        #region Pooling

        public static long TotalRentCount => _messagePool.TotalRentCount;
        public static long TotalReturnCount => _messagePool.TotalReturnCount;

        private static ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

        private const int PoolSizePerCpu = 8192;
        private static readonly  DefaultObjectPool<IncomingMessage> _messagePool =
            new DefaultObjectPool<IncomingMessage>(() => new IncomingMessage(), Environment.ProcessorCount * PoolSizePerCpu, PoolSizePerCpu);

        private Action<IncomingMessage> _returnToPoolAction;

        public void SetReturnToPoolAction(Action<IncomingMessage> returnToPoolAction)
        {
            _returnToPoolAction = returnToPoolAction;
        }

        public void Return()
        {
            if (_returnToPoolAction != null)
            {
                var returnToPoolAction = _returnToPoolAction;
                _returnToPoolAction = null;

                ResetForRecycling();

                returnToPoolAction(this);
            }
            else
            {
                Dispose();
            }
        }

        public long NextReusableTime { get; set; } = 0;

        public void Dispose()
        {
            ReleaseInternalBuffers();
        }

        private void ResetForRecycling()
        {
            MessageType = MessageType.None;
            Seq = null;
            Ack = null;
            SessionId = 0;
            UserSuid = 0;
            KeyIndex = 0;
            RemoteEncryptionKeyIndex = KeyIndex.None;
            RemoteEncryptionKey = null;
            ResultCode = NetworkResult.OK;
            CompressionType = CompressionType.None;
            Body = ArraySegment<byte>.Empty;

            ReleaseInternalBuffers();
        }

        private void ReleaseInternalBuffers()
        {
            if (_decryptedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_decryptedBuffer);
                _decryptedBuffer = null;
            }
        }
        #endregion

        public static int Parse(Memory<byte> data, KeyChain keyChain, out IncomingMessage message)
        {
            message = null;

            var span = data.Span;
            try
            {
                // Even the length of the message cannot be read, so return immediately
                if (span.Length < 2)
                {
                    return 0;
                }

                // Get the total message length including header and body.
                int messageLength = BinaryPrimitives.ReadUInt16LittleEndian(span);

                // Check if the message length is safe
                if (messageLength < MessageConstants.InitialHeaderLength || messageLength > MessageConstants.MaxMessageLength)
                {
                    throw new InvalidProtocolException($"Invalid message length={messageLength}");
                }

                // There is no data to read into the stream yet. Should be processed when it is filled more.
                if (messageLength > data.Length)
                {
                    return 0;
                }

                // Perform deserialization.
                int readedLength = Deserialize(data.Slice(0, messageLength), keyChain, out message);
                return readedLength;
            }
            catch (Exception)
            {
                if (message != null)
                {
                    message.Return();
                }

                throw;
            }
        }

        // Deserialize message.
        private static int Deserialize(ReadOnlyMemory<byte> memory, KeyChain keyChain, out IncomingMessage message)
        {
            message = _messagePool.Rent();

            var span = memory.Span;

            ValidateHeader(span);

            message.KeyIndex = (KeyIndex) span[2];
            message.MessageType = (MessageType) span[3];

            var flags = span[4];

            // Skip header
            span = span.Slice(MessageConstants.InitialHeaderLength);
            int readPosition = MessageConstants.InitialHeaderLength;

            // Seq?
            if ((flags & MessageConstants.HasSeqMask) != 0)
            {
                CheckRequiredLength(span, 4);

                message.Seq = BinaryPrimitives.ReadUInt32LittleEndian(span);
                span = span.Slice(4);
                readPosition += 4;
            }

            // Ack?
            if ((flags & MessageConstants.HasAckMask) != 0)
            {
                CheckRequiredLength(span, 4);

                message.Ack = BinaryPrimitives.ReadUInt32LittleEndian(span);
                span = span.Slice(4);
                readPosition += 4;
            }

            // SessionId?
            if ((flags & MessageConstants.HasSessionIdMask) != 0)
            {
                CheckRequiredLength(span, 8);

                message.SessionId = BinaryPrimitives.ReadInt64LittleEndian(span);
                span = span.Slice(8);
                readPosition += 8;
            }

            // UserSuid?
            if ((flags & MessageConstants.HasUserSuidMask) != 0)
            {
                CheckRequiredLength(span, 8);

                message.UserSuid = BinaryPrimitives.ReadInt64LittleEndian(span);
                span = span.Slice(8);
                readPosition += 8;
            }

            // RemoteKey?
            if ((flags & MessageConstants.HasRemoteKeyMask) != 0)
            {
                CheckRequiredLength(span, 1 + MessageConstants.EncryptionKeyLength);

                // RemoteEncryptionKeyIndex
                message.RemoteEncryptionKeyIndex = (KeyIndex) span[0];
                span = span.Slice(1);
                readPosition += 1;


                // RemoteEncryptionKey

                // As it is allocated only once and the message object is pooled, it does not adversely affect GC.
                message._remoteEncryptionKeyBuffer ??= new uint[MessageConstants.EncryptionKeyLengthInDWords];
                message.RemoteEncryptionKey = message._remoteEncryptionKeyBuffer;

                message.RemoteEncryptionKey[0] = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
                message.RemoteEncryptionKey[1] = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
                message.RemoteEncryptionKey[2] = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4));
                message.RemoteEncryptionKey[3] = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4));
                span = span.Slice(MessageConstants.EncryptionKeyLength);
                readPosition += MessageConstants.EncryptionKeyLength;
            }

            // ResultCode?
            if ((flags & MessageConstants.HasResultCodeMask) != 0)
            {
                CheckRequiredLength(span, 1);

                message.ResultCode = (NetworkResult) span[0];
                span = span.Slice(1);
                readPosition += 1;
            }

            // Body?
            if ((flags & MessageConstants.HasBodyMask) != 0)
            {
                CheckRequiredLength(span, 1); // Since it is already marked as having a body, it should be at least 1 byte.

                message.CompressionType = CompressionType.None;
                int uncompressedLength = 0;

                if ((flags & MessageConstants.HasCompressionMask) != 0)
                {
                    CheckRequiredLength(span, 1 + 4); // type(1) + uncompressedLength(4)

                    // compression-type
                    message.CompressionType = (CompressionType) span[0];
                    span = span.Slice(1);
                    readPosition += 1;

                    // uncompressed-length
                    uncompressedLength = BinaryPrimitives.ReadInt32LittleEndian(span);
                    span = span.Slice(4);
                    readPosition += 4;
                }

                message.Body = memory.Slice(readPosition);
                readPosition += message.Body.Length;

                // Decryption?
                if (message.KeyIndex != 0)
                {
                    (byte[] buffer, Memory<byte> memory) bm = (null, Memory<byte>.Empty);
                    try
                    {
                        bm = keyChain.DecryptUsingArrayPool(message.KeyIndex, message.Body);
                        message._decryptedBuffer = bm.buffer; // owned!
                        message.Body = bm.memory;
                    }
                    catch (Exception)
                    {
                        // MAC은 적용되지 않았군...?

                        if (bm.buffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(bm.buffer);
                        }

                        throw new InvalidProtocolException("Decryption failure");
                    }
                }

                // Decompression?
                if (message.CompressionType != CompressionType.None)
                {
                    //todo optimization(avoid array copy)
                    var uncompressed = Compressor.Decompress(message.CompressionType, message.Body.ToArray(), uncompressedLength);
                    //var uncompressed = Compressor.Decompress(message.CompressionType, message.Body, uncompressedLength);
                    message.Body = uncompressed;
                }
            }
            else
            {
                message.Body = ReadOnlyMemory<byte>.Empty;
            }

            // Check if you have read all the way to the end.
            // If there is still data left, someone has put the data randomly
            // and should be treated as an error.
            if (readPosition != memory.Length)
            {
                throw new InvalidProtocolException("Message rigging is detected.");
            }

            return readPosition;
        }

        public override string ToString()
        {
            var sb = Stringify.Begin();

            Stringify.Append(sb, "messageType", MessageType);

            if (Seq.HasValue)
                Stringify.Append(sb, "seq", Seq.Value);

            if (Ack.HasValue)
                Stringify.Append(sb, "ack", Ack.Value);

            if (SessionId != 0)
                Stringify.Append(sb, "sessionId", SessionId);

            Stringify.Append(sb, "keyIndex", KeyIndex);

            if (RemoteEncryptionKeyIndex != KeyIndex.None)
            {
                Stringify.Append(sb, "remoteEncryptionKeyIndex", RemoteEncryptionKeyIndex);
                Stringify.Append(sb, "remoteEncryptionKey[0]", RemoteEncryptionKey[0]);
                Stringify.Append(sb, "remoteEncryptionKey[1]", RemoteEncryptionKey[1]);
                Stringify.Append(sb, "remoteEncryptionKey[2]", RemoteEncryptionKey[2]);
                Stringify.Append(sb, "remoteEncryptionKey[3]", RemoteEncryptionKey[3]);
            }

            if (CompressionType != CompressionType.None)
                Stringify.Append(sb, "compressionType", CompressionType);

            if (ResultCode != NetworkResult.OK)
                Stringify.Append(sb, "resultCode", ResultCode);

            if (Body.Length > 0)
                Stringify.Append(sb, "body", Body.Length); //todo hex로 덤프시킬까?

            return Stringify.End(sb);
        }


        #region Validations
        private static void ValidateHeader(ReadOnlySpan<byte> header)
        {
            //todo
        }

        private static void CheckRequiredLength(ReadOnlySpan<byte> span, int requiredLength)
        {
            if (span.Length < requiredLength)
            {
                throw new InvalidProtocolException("Truncated message.");
            }
        }
        #endregion
    }
}
