using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace G.Util
{
	public static class Md5
	{
		public static byte[] GetHash(byte[] data)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(data);
			}
		}

		public static byte[] GetHash(byte[] data, int offset, int count)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(data, offset, count);
			}
		}

		public static byte[] GetHash(Stream stream)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(stream);
			}
		}

		public static byte[] GetHash(string text)
		{
			return GetHash(Encoding.UTF8.GetBytes(text));
		}

		public static string GetHashString(byte[] data)
		{
			return Hex.ToString(GetHash(data));
		}

		public static string GetHashString(string text)
		{
			return Hex.ToString(GetHash(Encoding.UTF8.GetBytes(text)));
		}

		public static bool Verify(byte[] data, byte[] hash)
		{
			byte[] hash2 = GetHash(data);

			if (hash.Length != hash2.Length) return false;

			for (int i = 0; i < hash.Length; i++)
			{
				if (hash[i] != hash2[i]) return false;
			}

			return true;
		}

		public static bool Verify(byte[] data, string hashString)
		{
			return Verify(data, Hex.FromString(hashString));
		}

		public static bool Verify(string text, byte[] hash)
		{
			return Verify(Encoding.UTF8.GetBytes(text), hash);
		}

		public static bool Verify(string text, string hashString)
		{
			return Verify(Encoding.UTF8.GetBytes(text), Hex.FromString(hashString));
		}
	}
}
