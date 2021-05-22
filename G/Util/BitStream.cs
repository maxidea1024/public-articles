using System;
using System.IO;

namespace G.Util
{
    public class BitStream : Stream
    {
        private byte[] Source { get; set; }

        public BitStream(int capacity)
        {
            Source = new byte[capacity];
        }

        public BitStream(byte[] source)
        {
            Source = source;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { return Source.Length * 8; }
        }

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long tempPos = Position;
            tempPos += offset;

            int readPosCount = 0, readPosMod = 0;

            long posCount = tempPos >> 3;
            int posMod = (int) (tempPos - ((tempPos >> 3) << 3));

            while (tempPos < Position + offset + count && tempPos < Length)
            {
                if ((((int) Source[posCount]) & (0x1 << (7 - posMod))) != 0)
                {
                    buffer[readPosCount] = (byte) ((int) (buffer[readPosCount]) | (0x1 << (7 - readPosMod)));
                }
                else
                {
                    buffer[readPosCount] =
                        (byte) ((int) (buffer[readPosCount]) & (0xffffffff - (0x1 << (7 - readPosMod))));
                }

                tempPos++;
                if (posMod == 7)
                {
                    posMod = 0;
                    posCount++;
                }
                else
                {
                    posMod++;
                }

                if (readPosMod == 7)
                {
                    readPosMod = 0;
                    readPosCount++;
                }
                else
                {
                    readPosMod++;
                }
            }

            int bits = (int) (tempPos - Position - offset);
            Position = tempPos;
            return bits;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case (SeekOrigin.Begin):
                {
                    Position = offset;
                    break;
                }
                case (SeekOrigin.Current):
                {
                    Position += offset;
                    break;
                }
                case (SeekOrigin.End):
                {
                    Position = Length + offset;
                    break;
                }
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            long tempPos = Position;

            int readPosCount = offset >> 3, readPosMod = offset - ((offset >> 3) << 3);

            long posCount = tempPos >> 3;
            int posMod = (int) (tempPos - ((tempPos >> 3) << 3));

            while (tempPos < Position + count && tempPos < Length)
            {
                if ((((int) buffer[readPosCount]) & (0x1 << (7 - readPosMod))) != 0)
                {
                    Source[posCount] = (byte) ((int) (Source[posCount]) | (0x1 << (7 - posMod)));
                }
                else
                {
                    Source[posCount] = (byte) ((int) (Source[posCount]) & (0xffffffff - (0x1 << (7 - posMod))));
                }

                tempPos++;
                if (posMod == 7)
                {
                    posMod = 0;
                    posCount++;
                }
                else
                {
                    posMod++;
                }

                if (readPosMod == 7)
                {
                    readPosMod = 0;
                    readPosCount++;
                }
                else
                {
                    readPosMod++;
                }
            }

            Position = tempPos;
        }
    }
}
