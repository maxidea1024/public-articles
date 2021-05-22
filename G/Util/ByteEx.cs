using System;

namespace G.Util
{
	public class ByteEx
	{
		public static byte[] ToBigEndian(short v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToLittleEndian(short v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToBigEndian(ushort v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToLittleEndian(ushort v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToBigEndian(int v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToLittleEndian(int v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToBigEndian(uint v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToLittleEndian(uint v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToBigEndian(long v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToLittleEndian(long v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToBigEndian(ulong v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public static byte[] ToLittleEndian(ulong v)
		{
			var bytes = BitConverter.GetBytes(v);
			if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}
	}
}
