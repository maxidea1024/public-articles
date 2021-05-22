using System.Collections.Generic;

namespace G.Util
{
	public class MemoryPool : Singleton<MemoryPool>
	{
		private Queue<byte[]> q16 = new Queue<byte[]>();
		private Queue<byte[]> q32 = new Queue<byte[]>();
		private Queue<byte[]> q64 = new Queue<byte[]>();
		private Queue<byte[]> q128 = new Queue<byte[]>();
		private Queue<byte[]> q256 = new Queue<byte[]>();
		private Queue<byte[]> q512 = new Queue<byte[]>();
		private Queue<byte[]> q1024 = new Queue<byte[]>();
		private Queue<byte[]> q2048 = new Queue<byte[]>();
		private Queue<byte[]> q4096 = new Queue<byte[]>();
		private Queue<byte[]> q8192 = new Queue<byte[]>();

		private byte[] CheckOut(Queue<byte[]> q, int size)
		{
			if (q.Count > 0)
				return q.Dequeue();
			else
				return new byte[size];
		}

		public void CheckIn(byte[] buffer)
		{
			Queue<byte[]> queue = null;
			switch (buffer.Length)
			{
			case 16: queue = q16; break;
			case 32: queue = q32; break;
			case 64: queue = q64; break;
			case 128: queue = q128; break;
			case 256: queue = q256; break;
			case 512: queue = q512; break;
			case 1024: queue = q1024; break;
			case 2048: queue = q2048; break;
			case 4096: queue = q4096; break;
			case 8192: queue = q8192; break;
			default: return;
			}

			lock (queue)
			{
				queue.Enqueue(buffer);
			}
		}

		public byte[] CheckOut16()
		{
			lock (q16) { return CheckOut(q16, 16); }
		}

		public byte[] CheckOut32()
		{
			lock (q32) { return CheckOut(q32, 32); }
		}

		public byte[] CheckOut64()
		{
			lock (q64) { return CheckOut(q64, 64); }
		}

		public byte[] CheckOut128()
		{
			lock (q128) { return CheckOut(q128, 128); }
		}

		public byte[] CheckOut256()
		{
			lock (q256) { return CheckOut(q256, 256); }
		}

		public byte[] CheckOut512()
		{
			lock (q512) { return CheckOut(q512, 512); }
		}

		public byte[] CheckOut1024()
		{
			lock (q1024) { return CheckOut(q1024, 1024); }
		}

		public byte[] CheckOut2048()
		{
			lock (q2048) { return CheckOut(q2048, 2048); }
		}

		public byte[] CheckOut4096()
		{
			lock (q4096) { return CheckOut(q4096, 4096); }
		}

		public byte[] CheckOut8192()
		{
			lock (q8192) { return CheckOut(q8192, 8192); }
		}

		public byte[] CheckOut(int size)
		{
			if (size <= 16) return CheckOut16();
			if (size <= 32) return CheckOut32();
			if (size <= 64) return CheckOut64();
			if (size <= 128) return CheckOut128();
			if (size <= 256) return CheckOut256();
			if (size <= 512) return CheckOut512();
			if (size <= 1024) return CheckOut1024();
			if (size <= 2048) return CheckOut2048();
			if (size <= 4096) return CheckOut4096();
			if (size <= 8192) return CheckOut8192();
			return null;
		}
	}
}
