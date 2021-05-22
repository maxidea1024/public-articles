using System;

namespace G.Network.Messaging
{
    /// <summary>
    /// Define constants related to messaging
    /// </summary>
    public static class MessageConstants
    {
        /// <summary>Fixed buffer length for headers</summary>
        public const int HeaderBufferLength = 64;

        /// <summary>Basic message header length (unconditionally occupies this length)</summary>
        public const int InitialHeaderLength = 5;

        /// <summary>Maximum message length (including header + body)</summary>
        public const int MaxMessageLength = 32767;

        /// <summary>The default buffer length to use when pulling packets out of the stream</summary>
        public const int DefaultDequeueMessageBufferLength = 1024;

        /// <summary>Symmetric encryption key length(in dwords)</summary>
        public const int EncryptionKeyLengthInDWords = 4;
        /// <summary>Symmetric encryption key length(in bytes)</summary>
        public const int EncryptionKeyLength = EncryptionKeyLengthInDWords * 4;


        /// <summary>Flag indicating whether the seq field is included in the message</summary>
        public const byte HasSeqMask = 0x80;

        /// <summary>Flag indicating whether the ack field is included in the message</summary>
        public const byte HasAckMask = 0x40;

        /// <summary>Flag indicating whether the session-id field is included in the message</summary>
        public const byte HasSessionIdMask = 0x20;

        /// <summary>Flag indicating whether body is included in message</summary>
        public const byte HasBodyMask = 0x10;

        /// <summary>Flag indicating whether the remote encryption key is included in the message</summary>
        public const byte HasRemoteKeyMask = 0x08;

        /// <summary>Flag indicating whether the result code is included in the message</summary>
        public const byte HasResultCodeMask = 0x04;

        /// <summary>Flag indicating whether to compress the message</summary>
        public const byte HasCompressionMask = 0x02;
    }
}
