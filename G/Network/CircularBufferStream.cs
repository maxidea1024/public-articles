using System;
using System.Collections.Generic;
using System.IO;

namespace G.Network
{
    public class CircularBufferStream : Stream
    {
        private byte[] buffer;

        private int capacity;
        public int Capacity { get { return capacity; } }

        private int readable;
        public int Readable { get { return readable; } }

        public int Writable { get { return capacity - readable; } }

        private int offsetR;
        private int offsetW;

        public CircularBufferStream(int capacity)
        {
            this.capacity = capacity;
            buffer = new byte[capacity];
            readable = 0;
            offsetR = 0;
            offsetW = 0;
        }

        public int Peek(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException();
            }

            if (offset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (offset + count > buffer.Length)
            {
                throw new ArgumentException();
            }

            if (count > readable)
            {
                count = readable;
            }

            int count2 = offsetR + count - capacity;
            if (count2 > 0)
            {
                int count1 = count - count2;

                Array.Copy(this.buffer, offsetR, buffer, offset, count1);
                Array.Copy(this.buffer, 0, buffer, offset + count1, count2);
            }
            else
            {
                Array.Copy(this.buffer, offsetR, buffer, offset, count);
            }

            return count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Peek(buffer, offset, count);

            offsetR = (offsetR + count) % capacity;
            readable -= count;

            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException();
            }

            if (offset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (offset + count > buffer.Length)
            {
                throw new ArgumentException();
            }

            if (count > Writable)
            {
                throw new ArgumentException();
            }

            int count2 = offsetW + count - capacity;
            if (count2 > 0)
            {
                int count1 = count - count2;

                Array.Copy(buffer, offset, this.buffer, offsetW, count1);
                Array.Copy(buffer, offset + count1, this.buffer, 0, count2);
            }
            else
            {
                Array.Copy(buffer, offset, this.buffer, offsetW, count);
            }

            offsetW = (offsetW + count) % capacity;
            readable += count;
        }

        public override bool CanRead { get { return true; } }

        public override bool CanWrite { get { return true; } }

        public override bool CanSeek { get { return false; } }

        public override void Flush()
        {
            readable = 0;
            offsetR = 0;
            offsetW = 0;
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        #region For Overlapped IO
        private List<ArraySegment<byte>> readableSegments = new List<ArraySegment<byte>>();
        private List<ArraySegment<byte>> writableSegments = new List<ArraySegment<byte>>();

        public List<ArraySegment<byte>> ReadableSegments
        {
            get
            {
                readableSegments.Clear();
                if (readable <= 0) return readableSegments;

                int count2 = offsetR + readable - capacity;
                if (count2 > 0)
                {
                    readableSegments.Add(new ArraySegment<byte>(buffer, offsetR, readable - count2));
                    readableSegments.Add(new ArraySegment<byte>(buffer, 0, count2));
                }
                else
                {
                    readableSegments.Add(new ArraySegment<byte>(buffer, offsetR, readable));
                }

                return readableSegments;
            }
        }

        public bool OnRead(int count)
        {
            if (count > readable) return false;
            offsetR = (offsetR + count) % capacity;
            readable -= count;
            return true;
        }

        public List<ArraySegment<byte>> WritableSegments
        {
            get
            {
                writableSegments.Clear();

                int writable = this.Writable;
                if (writable <= 0) return writableSegments;

                int count2 = offsetW + writable - capacity;
                if (count2 > 0)
                {
                    writableSegments.Add(new ArraySegment<byte>(buffer, offsetW, writable - count2));
                    writableSegments.Add(new ArraySegment<byte>(buffer, 0, count2));
                }
                else
                {
                    writableSegments.Add(new ArraySegment<byte>(buffer, offsetW, writable));
                }

                return writableSegments;
            }
        }

        public bool OnWrite(int count)
        {
            if (count > Writable) return false;
            offsetW = (offsetW + count) % capacity;
            readable += count;
            return true;
        }
        #endregion

        #region State
        public int backupReadable = -1;
        public int backupOffsetR = -1;
        public int backupOffsetW = -1;

        public void Save()
        {
            backupReadable = readable;
            backupOffsetR = offsetR;
            backupOffsetW = offsetW;
        }

        public void Restore()
        {
            readable = backupReadable;
            offsetR = backupOffsetR;
            offsetW = backupOffsetW;
        }
		#endregion

		#region IDisposable
		private bool disposed = false;

		~CircularBufferStream()
		{
			Dispose(false);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposed) return;

			if (disposing)
			{
				buffer = null;
			}

			disposed = true;

			base.Dispose(disposing);
		}
		#endregion
	}
}
