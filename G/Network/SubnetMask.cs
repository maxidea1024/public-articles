using System;

namespace G.Network
{
	public class SubnetMask
	{
		private int bit;

		public int Bit
		{
			get { return bit; }
			set
			{
				if (value < 0 || value > 32)
					throw new ArgumentOutOfRangeException("Bit");
				bit = value;
			}
		}

		public long Number
		{
			get { return BitToNumber(bit); }
			set { bit = NumberToBit(value); }
		}

		public SubnetMask() {}

		public SubnetMask(int bit)
		{
			this.bit = bit;
		}

		public SubnetMask(byte b1, byte b2, byte b3, byte b4)
		{
			bit = BytesToBit(new byte[] { b1, b2, b3, b4 });
		}

		public SubnetMask(byte[] bytes)
		{
			bit = BytesToBit(bytes);
		}

		public byte[] GetBytes()
		{
			return BitToBytes(bit);
		}

		public void Set(int bit)
		{
			Bit = bit;
		}

		public void Set(byte b1, byte b2, byte b3, byte b4)
		{
			bit = BytesToBit(new byte[] { b1, b2, b3, b4 });
		}

		public void Set(byte[] bytes)
		{
			bit = BytesToBit(bytes);
		}

		public void SetNumber(long num)
		{
			bit = NumberToBit(num);
		}

		public override string ToString()
		{
			return BitToString(bit);
		}

		public string ToStringInverse()
		{
			return BitToStringInverse(bit);
		}

		// 23 --> 512
		// 17 --> 32768
		public static long BitToNumber(int bit)
		{
			return (long)Math.Pow(2, 32 - bit);
		}

		// 23 --> { 255, 255, 254, 0 }
		// 17 --> { 255, 255, 128, 0 }
		public static byte[] BitToBytes(int bit)
		{
			long num = (long)Math.Pow(2, 32 - bit);
			byte[] m = BitConverter.GetBytes(~(num - 1));
			return new byte[] { m[3], m[2], m[1], m[0] };
		}

		// 23 --> "255.255.254.0"
		// 17 --> "255.255.128.0"
		public static string BitToString(int bit)
		{
			long num = (long)Math.Pow(2, 32 - bit);
			byte[] m = BitConverter.GetBytes(~(num - 1));
			return String.Format("{0}.{1}.{2}.{3}", m[3], m[2], m[1], m[0]);
		}

		// 23 --> "0.0.1.255"
		// 17 --> "0.0.127.255"
		public static string BitToStringInverse(int bit)
		{
			long num = (long)Math.Pow(2, 32 - bit);
			byte[] m = BitConverter.GetBytes(num - 1);
			return String.Format("{0}.{1}.{2}.{3}", m[3], m[2], m[1], m[0]);
		}

		// 512 --> 23
		// 32768 --> 17
		public static int NumberToBit(long num)
		{
			return 32 - (int)Math.Log(num, 2);
		}

		// 512 --> { 255, 255, 254, 0 }
		// 32768 --> { 255, 255, 128, 0 }
		public static byte[] NumberToBytes(long num)
		{
			byte[] m = BitConverter.GetBytes(~(num - 1));
			return new byte[] { m[3], m[2], m[1], m[0] };
		}

		// 512 --> "255.255.254.0"
		// 32768 --> "255.255.128.0"
		public static string NumberToString(long num)
		{
			byte[] m = BitConverter.GetBytes(~(num - 1));
			return String.Format("{0}.{1}.{2}.{3}", m[3], m[2], m[1], m[0]);
		}

		// 512 --> "0.0.1.255"
		// 32768 --> "0.0.127.255"
		public static string NumberToStringInverse(long num)
		{
			byte[] m = BitConverter.GetBytes(num - 1);
			return String.Format("{0}.{1}.{2}.{3}", m[3], m[2], m[1], m[0]);
		}

		// { 255, 255, 254, 0 } --> 23
		// { 255, 255, 128, 0 } --> 17
		public static int BytesToBit(byte[] bytes)
		{
			return NumberToBit(BytesToNumber(bytes));
		}

		// { 255, 255, 254, 0 } --> 512
		// { 255, 255, 128, 0 } --> 32768
		public static long BytesToNumber(byte[] bytes)
		{
			byte[] m = new byte[4];
			m[3] = (byte)~bytes[0];
			m[2] = (byte)~bytes[1];
			m[1] = (byte)~bytes[2];
			m[0] = (byte)~bytes[3];
			return (long)BitConverter.ToUInt32(m, 0) + 1;
		}

		// { 255, 255, 254, 0 } --> "255.255.254.0"
		// { 255, 255, 128, 0 } --> "255.255.128.0"
		public static string BytesToString(byte[] bytes)
		{
			return String.Format("{0}.{1}.{2}.{3}", bytes[0], bytes[1], bytes[2], bytes[3]);
		}

		// { 255, 255, 254, 0 } --> "0.0.1.255"
		// { 255, 255, 128, 0 } --> "0.0.127.255"
		public static string BytesToStringInverse(byte[] bytes)
		{
			return String.Format("{0}.{1}.{2}.{3}", (byte)~bytes[0], (byte)~bytes[1], (byte)~bytes[2], (byte)~bytes[3]);
		}
	}
}
