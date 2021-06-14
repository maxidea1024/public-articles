using System;
using System.IO;
using System.Collections.Generic;

namespace G.Network
{
	public class LinearBufferStream : Stream
	{
		private byte[] buffer;

		private int capacity;
		public int Capacity { get { return capacity; } }

		private int readable;
		public int Readable { get { return readable; } }

		public int Writable { get { return capacity - offsetW; } }

		private int offsetR;
		private int offsetW;


		private int _initialCapacity;

		public LinearBufferStream(int capacity)
		{
			if (capacity < 8192)
				capacity = 8192;

			_initialCapacity = capacity;
			this.capacity = capacity;
			buffer = new byte[capacity];
			readable = 0;
			offsetR = 0;
			offsetW = 0;
		}

		public int Peek(byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException();

			if (offset < 0 || count < 0)
				throw new ArgumentOutOfRangeException();

			if (offset + count > buffer.Length)
				throw new ArgumentException();

			if (count > readable)
				count = readable;

			Array.Copy(this.buffer, offsetR, buffer, offset, count);

			return count;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			count = Peek(buffer, offset, count);

			offsetR += count;
			readable -= count;

			return count;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			Write(buffer.AsSpan(offset, count));
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException();

			int count = buffer.Length;

			//if (count > Writable)
			// throw new ArgumentException();
			EnsureForWritable(count);

			buffer.CopyTo(this.buffer.AsSpan(offsetW, count));

			offsetW += count;
			readable += count;
		}

		//maxidea
		public void EnsureForWritable(int lengthToWrite)
		{
			if (Writable < lengthToWrite)
			{
				int newCapacity = GetOptimalWritableBufferLength(this.capacity + lengthToWrite - Writable);
				Array.Resize(ref this.buffer, newCapacity);
				this.capacity = newCapacity;
			}
		}

		private int GetOptimalWritableBufferLength(int desiredCapacity)
		{
			// 32k, 64k, 128k ...
			int newCapacity = _initialCapacity;
			while (true)
			{
				if (desiredCapacity < newCapacity)
					break;

				newCapacity *= 2;
			}

			return newCapacity;
		}

		public override bool CanRead { get { return true; } }

		public override bool CanWrite { get { return true; } }

		public override bool CanSeek { get { return false; } }

		public override void Flush()
		{
			Reset();
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

		public ArraySegment<byte> ReadableSegment
		{
			get
			{
				return new ArraySegment<byte>(buffer, offsetR, readable);
			}
		}

		public List<ArraySegment<byte>> ReadableSegments
		{
			get
			{
				return new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer, offsetR, readable) };
			}
		}

		public ReadOnlyMemory<byte> ReadableMemory
		{
			get
			{
				return new ReadOnlyMemory<byte>(buffer, offsetR, readable);
			}
		}

		public ReadOnlySpan<byte> ReadableSpan
		{
			get
			{
				return new ReadOnlySpan<byte>(buffer, offsetR, readable);
			}
		}

		public bool OnRead(int count)
		{
			if (count > readable) return false;

			readable -= count;
			if (readable == 0)
			{
				offsetR = 0;
				offsetW = 0;
			}
			else
			{
				offsetR += count;
			}

			return true;
		}

		public ArraySegment<byte> WritableSegment
		{
			get
			{
				int writable = this.Writable;
				if (writable <= 0) return null;

				return new ArraySegment<byte>(buffer, offsetW, writable);
			}
		}

		public List<ArraySegment<byte>> WritableSegments
		{
			get
			{
				int writable = this.Writable;
				if (writable <= 0) return null;

				return new List<ArraySegment<byte>> { new ArraySegment<byte>(buffer, offsetW, writable) };
			}
		}

		public Memory<byte> WritableMemory
		{
			get
			{
				int writable = this.Writable;

				if (writable <= 0)
				{
					return default(Memory<byte>);
				}

				return new Memory<byte>(buffer, offsetW, writable);
			}
		}

		public Span<byte> WritableSpan
		{
			get
			{
				int writable = this.Writable;

				if (writable <= 0)
				{
					return default(Span<byte>);
				}

				return new Span<byte>(buffer, offsetW, writable);
			}
		}

		public bool OnWrite(int count)
		{
			if (count > Writable) return false;
			offsetW += count;
			readable += count;
			return true;
		}

		public void Reset()
		{
			if (_initialCapacity != this.capacity)
            {
				this.capacity = _initialCapacity;
				buffer = new byte[capacity];
			}

			readable = 0;
			offsetR = 0;
			offsetW = 0;
		}

		public void Optimize()
		{
			if (readable > 0 && offsetR > 0)
			{
				Array.Copy(buffer, offsetR, buffer, 0, readable);
				offsetR = 0;
				offsetW = readable;
			}
		}

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

		~LinearBufferStream()
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

		//--------------
		// PalyTogether override
		public int OffsetR { get { return offsetR; } }
		public int OffsetW { get { return offsetW; } }
		//--------------
	}
}
