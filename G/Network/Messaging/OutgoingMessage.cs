//todo 최소한의 정보만 Message에 유지하고 나머지는 필요한 상황에서만 다루는 걸로 변경하자.
//     즉, 하나의 메시지 타입으로 퉁치는것 보다는 나눠서 처리하는게 간단할듯 싶다.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using G.Util;
using G.Util.Compression;
using PlayTogether;

namespace G.Network.Messaging
{
    /// <summary>
    /// Message object for sending.
    /// </summary>
    public class OutgoingMessage : IRecycleable<OutgoingMessage>
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>whether to encode the message</summary>
        public bool IsEncoded { get; set; }

        /// <summary>Message type</summary>
        public MessageType MessageType { get; set; } = MessageType.User;

        /// <summary>Result code</summary>
        public NetworkResult ResultCode { get; set; }

        /// <summary>Message Body</summary>
        public BaseProtocol Body { get; set; }

        /// <summary>Key index for encryption</summary>
        public KeyIndex KeyIndex { get; set; }

        /// <summary>Remote encryption key (16 bytes) Common key must be set separately.</summary>
        public KeyIndex RemoteEncryptionKeyIndex { get; set; }

        /// <summary>message seq number</summary>
        public uint? Seq { get; set; }

        /// <summary>ack to reply to receive message</summary>
        public uint? Ack { get; set; }

        /// <summary>Session ID to identify session</summary>
        public long SessionId { get; set; }

        public long UserSuid { get; set; }

        /// <summary>Compression type</summary>
        public CompressionType CompressionType { get; set; }

        /// <summary>encoded (transmittable over the network) header</summary>
        public ArraySegment<byte> PackedHeader { get; set; }

        /// <summary>Encoded (transmittable over the network) body</summary>
        public ArraySegment<byte> PackedBody { get; set; }

        // UniqueKey(NonSerialized)
        public uint UniqueKey { get; set; }

        // Mutable header and body.
        internal ArraySegment<byte> SendableHeader { get; set; }
        internal ArraySegment<byte> SendableBody { get; set; }

        // Internal message header buffer
        private readonly byte[] _headerBuffer = new byte[MessageConstants.HeaderBufferLength];
        private CompressionType _appliedCompressionType = CompressionType.None;
        private int _uncompressedBodyLength = 0;
        private ArraySegment<byte> _originalPackedBody; // for Rebuild


        #region Pooling
        public static long TotalRentCount => _messagePool.TotalRentCount;
        public static long TotalReturnCount => _messagePool.TotalReturnCount;

        private const int PoolSizePerCpu = 8192;
        private static readonly  DefaultObjectPool<OutgoingMessage> _messagePool =
            new DefaultObjectPool<OutgoingMessage>(() => new OutgoingMessage(), Environment.ProcessorCount * PoolSizePerCpu, PoolSizePerCpu);

        private Action<OutgoingMessage> _returnToPoolAction;

        public static object DebugListLock = new object();
        public static List<OutgoingMessage> DebugList = new List<OutgoingMessage>();

        public void SetReturnToPoolAction(Action<OutgoingMessage> returnToPoolAction)
        {
            _returnToPoolAction = returnToPoolAction;
        }

        public static OutgoingMessage Rent()
        {
           var rented = _messagePool.Rent();

            // For debugging
           lock (DebugListLock)
           {
                DebugList.Add(rented);
            }

            return rented;
        }

        public void Return()
        {
            // For debugging
            lock (DebugListLock)
            {
                DebugList.Remove(this);
            }

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
            // Do nothing
        }

        private void ResetForRecycling()
        {
            IsEncoded = false;
            MessageType = MessageType.User;
            ResultCode = NetworkResult.OK;
            KeyIndex = KeyIndex.None;
            RemoteEncryptionKeyIndex = KeyIndex.None;
            Seq = null;
            Ack = null;
            SessionId = 0;
            UserSuid = 0;
            CompressionType = CompressionType.None;
            Body = null;
            PackedHeader = ArraySegment<byte>.Empty;
            PackedBody = ArraySegment<byte>.Empty;
            _appliedCompressionType = CompressionType.None;
            _uncompressedBodyLength = 0;

            UniqueKey = 0;

            SendableHeader = ArraySegment<byte>.Empty;
            SendableBody = ArraySegment<byte>.Empty;
        }

        #endregion

        //@todo bodySerializer없이 구동 가능하도록 해야함.
        internal void Build(Action<Stream, object> bodySerializer, KeyChain keyChain)
        {
            //todo 일단은 무조건 되게 하기 위해서 금지
            KeyIndex = KeyIndex.None;

            PackedBody = ArraySegment<byte>.Empty;
            _originalPackedBody = ArraySegment<byte>.Empty;

            _appliedCompressionType = CompressionType.None;
            _uncompressedBodyLength = 0;

            if (Body != null)
            {
                // Serialize body
                using (var stream = new MemoryStream())
                {
                    bodySerializer(stream, Body);

                    PackedBody = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Position);
                }

                // Save for rebuild
                _originalPackedBody = PackedBody;

                // Compression?
                if (CompressionType != CompressionType.None)
                {
                    int compressionThreshold = Compressor.GetCompressionThreshold(CompressionType);
                    if (PackedBody.Count >= compressionThreshold)
                    {
                        // Perform compression
                        var compressed = Compressor.Compress(CompressionType, PackedBody);
                        if (compressed.Count == 0) // 압축한 결과가 원본보다 크면 길이가 0인 결과를 반환함.
                        {
                            _appliedCompressionType = CompressionType.None;
                        }
                        else
                        {
                            _appliedCompressionType = CompressionType;
                            _uncompressedBodyLength = PackedBody.Count;

                            // Replace body to compressed.
                            PackedBody = compressed;
                            //todo 압축 버퍼를 풀링했을 경우 풀에 반납해줘야하므로...
                            //_pooledCompressedBuffer = compressed.Array;
                        }
                    }
                }

                // 암호화 여부.
                if (KeyIndex != KeyIndex.None)
                {
                    (byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);
                    try
                    {
                        bm = keyChain.EncryptUsingArrayPool(KeyIndex, PackedBody.AsMemory());
                        if (bm.Buffer != null)
                        {
                            //todo optimization
                            PackedBody = bm.Memory.ToArray();
                        }
                    }
                    finally
                    {
                        if (bm.Buffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(bm.Buffer);
                        }
                    }
                }
            }

            // Build header
            BuildHeader(keyChain);

            SendableHeader = PackedHeader;
            SendableBody = PackedBody;

            IsEncoded = true;
        }

        internal void Rebuild(KeyChain keyChain)
        {
            if (!IsEncoded)
            {
                return;
            }

            // No encryption, so no need to rebuild.
            if (KeyIndex == KeyIndex.None || _originalPackedBody.Count == 0)
            {
                return;
            }

            PackedBody = _originalPackedBody;

            // Recompression
            if (_appliedCompressionType != CompressionType.None)
            {
                // Perform compression
                var compressed = Compressor.Compress(CompressionType, PackedBody);

                // Replace body to compressed.
                PackedBody = compressed;

                //todo 압축 버퍼를 풀링했을 경우 풀에 반납해줘야하므로...
                //기존에 압축했던 버퍼는 풀에 반납해야함!
                //_pooledCompressedBuffer = compressed.Array;
            }

            // Reencryption
            (byte[] Buffer, Memory<byte> Memory) bm = (null, Memory<byte>.Empty);
            try
            {
                bm = keyChain.EncryptUsingArrayPool(KeyIndex, PackedBody.AsMemory());
                if (bm.Buffer != null)
                {
                    //todo optimization
                    PackedBody = bm.Memory.ToArray();
                }
            }
            finally
            {
                if (bm.Buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(bm.Buffer);
                }
            }

            // No need to rebuild the header.
            //BuildHeader(keyChain);

            SendableHeader = PackedHeader;
            SendableBody = PackedBody;
        }

        private void BuildHeader(KeyChain keyChain)
        {
            byte flags = 0;

            var headerLength = MessageConstants.InitialHeaderLength;
            var span = _headerBuffer.AsSpan().Slice(headerLength);

            // Seq?
            if (Seq.HasValue)
            {
                flags |= MessageConstants.HasSeqMask;

                BinaryPrimitives.WriteUInt32LittleEndian(span, Seq.Value);
                span = span.Slice(4);
                headerLength += 4;
            }

            // Ack?
            if (Ack.HasValue)
            {
                flags |= MessageConstants.HasAckMask;

                BinaryPrimitives.WriteUInt32LittleEndian(span, Ack.Value);
                span = span.Slice(4);
                headerLength += 4;
            }

            // SessionId?
            if (SessionId != 0)
            {
                flags |= MessageConstants.HasSessionIdMask;

                BinaryPrimitives.WriteInt64LittleEndian(span, SessionId);
                span = span.Slice(8);
                headerLength += 8;
            }

            // UserSuid?
            if (UserSuid != 0)
            {
                flags |= MessageConstants.HasUserSuidMask;

                BinaryPrimitives.WriteInt64LittleEndian(span, UserSuid);
                span = span.Slice(8);
                headerLength += 8;
            }

            // RemoteKey?
            if (RemoteEncryptionKeyIndex != KeyIndex.None &&
                RemoteEncryptionKeyIndex != KeyIndex.Common)
            {
                var remoteEncryptionKey = keyChain.GetKey(RemoteEncryptionKeyIndex);
                if (remoteEncryptionKey != null)
                {
                    flags |= MessageConstants.HasRemoteKeyMask;

                    span[0] = (byte)RemoteEncryptionKeyIndex;
                    span = span.Slice(1);
                    headerLength += 1;

                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), remoteEncryptionKey[0]);
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), remoteEncryptionKey[1]);
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), remoteEncryptionKey[2]);
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), remoteEncryptionKey[3]);
                    span = span.Slice(MessageConstants.EncryptionKeyLength);
                    headerLength += MessageConstants.EncryptionKeyLength;
                }
            }

            // ResultCode?
            if (ResultCode != NetworkResult.OK)
            {
                flags |= MessageConstants.HasResultCodeMask;

                span[0] = (byte) ResultCode;
                span = span.Slice(1);
                headerLength++;
            }

            // Body?
            if (PackedBody.Count > 0)
            {
                flags |= MessageConstants.HasBodyMask;
            }

            // Compression?
            if (_appliedCompressionType != CompressionType.None)
            {
                flags |= MessageConstants.HasCompressionMask;

                span[0] = (byte) _appliedCompressionType;
                span = span.Slice(1);
                headerLength++;

                BinaryPrimitives.WriteInt32LittleEndian(span, _uncompressedBodyLength);
                span = span.Slice(4);
                headerLength += 4;
            }

            // Finalize

            if (headerLength > MessageConstants.HeaderBufferLength)
            {
                throw new InvalidProtocolException(
                    $"The maximum message header length exceeded. headerLength={headerLength}, headerBufferLength={MessageConstants.HeaderBufferLength}");
            }

            var headerAndBodyLength = headerLength + PackedBody.Count;
            if (headerAndBodyLength > MessageConstants.MaxMessageLength)
            {
                throw new InvalidProtocolException(
                    $"The maximum message length has been exceeded. length={headerAndBodyLength}, maxHeaderLength={MessageConstants.MaxMessageLength}");
            }

            _headerBuffer[0] = (byte) headerAndBodyLength;
            _headerBuffer[1] = (byte) (headerAndBodyLength >> 8);
            _headerBuffer[2] = (byte) KeyIndex;
            _headerBuffer[3] = (byte) MessageType;
            _headerBuffer[4] = flags;

            PackedHeader = new ArraySegment<byte>(_headerBuffer, 0, headerLength);
        }

        public override string ToString()
        {
            var sb = Stringify.Begin();

            if (MessageType != MessageType.None)
                Stringify.Append(sb, "MessageType", MessageType);

            if (Body != null)
                Stringify.Append(sb, "Body", Body);

            if (KeyIndex != KeyIndex.None)
                Stringify.Append(sb, "KeyIndex", KeyIndex);

            if (RemoteEncryptionKeyIndex != KeyIndex.None)
                Stringify.Append(sb, "RemoteEncryptionKeyIndex", RemoteEncryptionKeyIndex);

            if (Seq != null)
                Stringify.Append(sb, "Seq", Seq.Value);

            if (Ack != null)
                Stringify.Append(sb, "Ack", Ack.Value);

            if (SessionId != 0)
                Stringify.Append(sb, "SessionId", SessionId);

            if (UserSuid != 0)
                Stringify.Append(sb, "UserSuid", UserSuid);

            if (CompressionType != CompressionType.None)
                Stringify.Append(sb, "CompressionType", CompressionType);

            return Stringify.End(sb);
        }
    }
}
