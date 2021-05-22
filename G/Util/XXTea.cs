using System;
using System.Text;
using System.IO;
using System.Buffers;
using System.Runtime.InteropServices;

namespace G.Util
{
	public class XXTea
	{
		//private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		private static readonly uint DELTA = 0x9e3779b9;
		private static readonly int MaxEncryptedSize = 1_000_000;

		private uint[] k;
		public uint[] Key { get { return k; } }

		public XXTea()
		{
			SetKey();
		}

		public XXTea(uint[] key)
		{
			SetKey(key);
		}

		public XXTea(string key)
		{
			SetKey(key);
		}

		public void SetKey()
		{
			k = new uint[4];

			Random random = new Random();
			k[0] = (uint)random.Next();
			k[1] = (uint)random.Next();
			k[2] = (uint)random.Next();
			k[3] = (uint)random.Next();
		}

		public void SetKey(uint[] key)
		{
			int len = key.Length;
			int fixedLen = 4;

			k = new uint[fixedLen];
			for (int i = 0; i < fixedLen; i++)
			{
				if (i < len)
					k[i] = key[i];
				else
					k[i] = 0;
			}
		}

		public bool SetKey(string base62Key)
		{
			if (base62Key == null) return false;

			try
			{
				SetKey(ConvertEx.ToKey(base62Key));
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		public byte[] Encrypt(byte[] data, int offset, int count)
		{
			return Encrypt(new ReadOnlyMemory<byte>(data, offset, count));
		}

		public byte[] Encrypt(ReadOnlyMemory<byte> memory)
		{
			try
			{
				int count = memory.Length;
				int vSize = (int)Math.Ceiling((double)(count + 4) / 4);
				int bSize = vSize * 4;
				byte[] buffer = new byte[bSize];

				Span<byte> b = buffer;
				Span<uint> v = MemoryMarshal.Cast<byte, uint>(b);

				v[0] = (uint)count;
				memory.CopyTo(new Memory<byte>(buffer, 4, count));

				_Encrypt(v, k);

				return buffer;
			}
			catch
			{
				return null;
			}
		}

		public (byte[] Buffer, Memory<byte> Memory) EncryptUsingArrayPool(ReadOnlyMemory<byte> memory)
		{
			byte[] buffer = null;

			try
			{
				int count = memory.Length;
				int vSize = (int)Math.Ceiling((double)(count + 4) / 4);
				int bSize = vSize * 4;

				buffer = ArrayPool<byte>.Shared.Rent(bSize);
				var mem = buffer.AsMemory(0, bSize);

				Span<byte> b = mem.Span;
				Span<uint> v = MemoryMarshal.Cast<byte, uint>(b);

				v[0] = (uint)count;
				memory.CopyTo(mem.Slice(4, count));

				_Encrypt(v, k);

				return (buffer, mem);
			}
			catch
			{
				if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
				return (null, Memory<byte>.Empty);
			}
		}

		public byte[] Decrypt(byte[] data, int offset, int count)
		{
			return Decrypt(new ReadOnlyMemory<byte>(data, offset, count));
		}

		public byte[] Decrypt(ReadOnlyMemory<byte> memory)
		{
			int count = memory.Length;
			if (count > MaxEncryptedSize)
			{
				//log.Error($"XXTea.Decrypt : Over MaxEncryptedSize, count={count}");
				return null;
			}

			byte[] buffer = null;

			try
			{
				int vSize = (int)Math.Ceiling((double)count / 4);
				int bSize = vSize * 4;
				buffer = ArrayPool<byte>.Shared.Rent(bSize);

				Span<byte> b = buffer.AsSpan(0, bSize);
				Span<uint> v = MemoryMarshal.Cast<byte, uint>(b);

				memory.CopyTo(buffer);

				_Decrypt(v, k);

				return b.Slice(4, (int)v[0]).ToArray();
			}
			catch
			{
				return null;
			}
			finally
			{
				if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public (byte[] Buffer, Memory<byte> Memory) DecryptUsingArrayPool(ReadOnlyMemory<byte> memory)
		{
			int count = memory.Length;
			if (count > MaxEncryptedSize)
			{
				//log.Error($"XXTea.Decrypt : Over MaxEncryptedSize, count={count}");
				return (null, Memory<byte>.Empty);
			}

			byte[] buffer = null;

			try
			{
				int vSize = (int)Math.Ceiling((double)count / 4);
				int bSize = vSize * 4;

				buffer = ArrayPool<byte>.Shared.Rent(bSize);
				var mem = buffer.AsMemory();

				memory.CopyTo(mem);

				Span<byte> b = mem.Slice(0, bSize).Span;
				Span<uint> v = MemoryMarshal.Cast<byte, uint>(b);

				_Decrypt(v, k);

				return (buffer, mem.Slice(4, (int)v[0]));
			}
			catch
			{
				if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
				return (null, Memory<byte>.Empty);
			}
		}

		public byte[] Encrypt(byte[] data)
		{
			return Encrypt(data, 0, data.Length);
		}

		public byte[] Decrypt(byte[] data)
		{
			return Decrypt(data, 0, data.Length);
		}

		public byte[] Encrypt(string text, Encoding encoding)
		{
			byte[] data = encoding.GetBytes(text);
			return Encrypt(data, 0, data.Length);
		}

		public string Decrypt(byte[] data, Encoding encoding)
		{
			byte[] decrypted = Decrypt(data, 0, data.Length);
			return encoding.GetString(decrypted);
		}

		public string Decrypt(byte[] data, int offset, int count, Encoding encoding)
		{
			byte[] decrypted = Decrypt(data, offset, count);
			return encoding.GetString(decrypted);
		}

		public void EncryptToFile(string path, byte[] data)
		{
			byte[] encrypted = Encrypt(data, 0, data.Length);
			File.WriteAllBytes(path, encrypted);
		}

		public void EncryptToFile(string path, byte[] data, int offset, int count)
		{
			byte[] encrypted = Encrypt(data, offset, count);
			File.WriteAllBytes(path, encrypted);
		}

		public byte[] DecryptFromFile(string path)
		{
			byte[] encrypted = File.ReadAllBytes(path);
			return Decrypt(encrypted, 0, encrypted.Length);
		}

		public string EncryptString(string text)
		{
			if (text == null) return null;
			return Base62.ToBase62(Encrypt(text, Encoding.UTF8));
		}

		public string DecryptString(string text)
		{
			if (text == null) return null;
			return Decrypt(Base62.FromBase62(text), Encoding.UTF8);
		}

		private static void _Encrypt(Span<uint> v, uint[] k)
		{
			int n = v.Length;
			int p = 0;
			uint y = 0, sum = 0, e = 0;
			uint rounds = (uint)(6 + 52 / n);
			uint z = v[n - 1];

			do
			{
				sum += DELTA;
				e = (sum >> 2) & 3;
				for (p = 0; p < n - 1; p++)
				{
					y = v[p + 1];
					z = v[p] += (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (k[(p & 3) ^ e] ^ z)));
				}
				y = v[0];
				z = v[n - 1] += (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (k[(p & 3) ^ e] ^ z)));
			} while (--rounds > 0);
		}

		private static void _Decrypt(Span<uint> v, uint[] k)
		{
			int n = v.Length;
			int p = 0;
			uint z = 0, e = 0;
			uint rounds = (uint)(6 + 52 / n);
			uint sum = rounds * DELTA;
			uint y = v[0];

			do
			{
				e = (sum >> 2) & 3;
				for (p = n - 1; p > 0; p--)
				{
					z = v[p - 1];
					y = v[p] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (k[(p & 3) ^ e] ^ z)));
				}
				z = v[n - 1];
				y = v[0] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (k[(p & 3) ^ e] ^ z)));
			} while ((sum -= DELTA) != 0);
		}
	}
}
