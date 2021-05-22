using System;
using System.Text;

namespace G.Util
{
	public class Coupon
	{
		private static string couponTable = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
		private static byte[] couponTableReverse;

		static Coupon()
		{
			int num = 0;
			couponTableReverse = new byte[41];
			foreach (char ch in couponTable)
			{
				couponTableReverse[ch - 50] = (byte)num++;
			}
		}

		public static string ToString(byte[] buffer, int dash = 0)
		{
			int totalBit = buffer.Length * 8 / 5 * 5;

			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < totalBit; i += 5)
			{
				int index = i / 8;
				int rest = i % 8;
				int n;

				switch (rest)
				{
					case 0:
						n = (buffer[index] >> 3) & 0x1F;
						break;
					case 1:
						n = (buffer[index] >> 2) & 0x1F;
						break;
					case 2:
						n = (buffer[index] >> 1) & 0x1F;
						break;
					case 3:
						n = buffer[index] & 0x1F;
						break;
					case 4:
						n = ((buffer[index] << 1) | (buffer[index + 1] >> 7)) & 0x1F;
						break;
					case 5:
						n = ((buffer[index] << 2) | (buffer[index + 1] >> 6)) & 0x1F;
						break;
					case 6:
						n = ((buffer[index] << 3) | (buffer[index + 1] >> 5)) & 0x1F;
						break;
					case 7:
						n = ((buffer[index] << 4) | (buffer[index + 1] >> 4)) & 0x1F;
						break;
					default:
						n = 0;
						break;
				}

				if (dash > 0 && i != 0 && (i % dash) == 0) sb.Append('-');
				sb.Append(couponTable[n]);
			}

			return sb.ToString();
		}

		public static byte[] FromString(string s)
		{
			s = s.Replace("-", "").Replace(" ", "");
			s = s.ToUpper();

			byte[] buffer = new byte[(s.Length * 5 + 7) / 8];

			int i = 0;
			foreach (char ch in s)
			{
				int n = couponTableReverse[ch - 50];

				int index = i / 8;
				int rest = i % 8;

				switch (rest)
				{
					case 0:
						buffer[index] |= (byte)(n << 3);
						break;
					case 1:
						buffer[index] |= (byte)(n << 2);
						break;
					case 2:
						buffer[index] |= (byte)(n << 1);
						break;
					case 3:
						buffer[index] |= (byte)n;
						break;
					case 4:
						buffer[index] |= (byte)(n >> 1);
						buffer[index + 1] |= (byte)(n << 7);
						break;
					case 5:
						buffer[index] |= (byte)(n >> 2);
						buffer[index + 1] |= (byte)(n << 6);
						break;
					case 6:
						buffer[index] |= (byte)(n >> 3);
						buffer[index + 1] |= (byte)(n << 5);
						break;
					case 7:
						buffer[index] |= (byte)(n >> 4);
						buffer[index + 1] |= (byte)(n << 4);
						break;
				}

				i += 5;
			}

			return buffer;
		}
	}
}
