using System;

namespace G.Util
{
	// 0 ~ 0x0FFFFFFFFFFFFFFF
	public class SimpleEncrypter60
	{
		public long Seed { get; private set; } = 0x0C4E52ABE469DF37;

		private static long mask = 0x0FFFFFFFFFFFFFFF;
		private static int count = 12;
		private static int shift = 5;

        public SimpleEncrypter60() { }

        public SimpleEncrypter60(long seed)
        {
            Seed = seed;
        }

        private static long RotateLeft(long n, int bits)
		{
			return (n << bits) | (n >> (60 - bits));
		}

		private static long RotateRight(long n, int bits)
		{
			return (n >> bits) | (n << (60 - bits));
		}

		private static long ExchangeNibble(long n, int index)
		{
			var bytes = BitConverter.GetBytes(n);
			var b = bytes[index];
			b = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
			bytes[index] = b;
			return BitConverter.ToInt64(bytes, 0);
		}

		public long Encrypt(long n)
		{
			for (int i = 0; i < count; i++)
			{
				n = ExchangeNibble(n, 0);

				n ^= Seed;
				n = RotateLeft(n, shift);
				n &= mask;
			}

			return n;
		}

		public long Decrypt(long n)
		{
			for (int i = 0; i < count; i++)
			{
				n = RotateRight(n, shift);
				n ^= Seed;
				n &= mask;

				n = ExchangeNibble(n, 0);
			}

			return n;
		}
	}
}
