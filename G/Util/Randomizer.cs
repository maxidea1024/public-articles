using System;
using System.Collections.Generic;
using System.Text;

namespace G.Util
{
    public class Randomizer
    {
		private static readonly char[] characters;

		private Random random;

		static Randomizer()
		{
			StringBuilder sb = new StringBuilder();
			for (char ch = 'A'; ch <= 'Z'; ch++) sb.Append(ch);
			for (char ch = 'a'; ch <= 'z'; ch++) sb.Append(ch);
			for (char ch = '0'; ch <= '9'; ch++) sb.Append(ch);
			characters = sb.ToString().ToCharArray();
		}

		public Randomizer()
		{
			random = new Random();
		}

		public int Next() { return random.Next(); }
		public int Next(int max) { return random.Next(max); }
		public int Next(int min, int max) { return random.Next(min, max); }

		public uint NextUInt32() { return (uint)random.Next(); }

		public uint NextUInt32WithoutZero()
		{
			return (uint)NextWithoutZero();
		}

		public int NextPInt32()
		{
			Span<byte> buffer = stackalloc byte[4];
			NextBytes(buffer);
			buffer[3] &= 0x7F;
			return BitConverter.ToInt32(buffer);
		}

		public long NextInt64()
		{
			Span<byte> buffer = stackalloc byte[8];
			NextBytes(buffer);
			return BitConverter.ToInt64(buffer);
		}

		public ulong NextUInt64()
		{
			Span<byte> buffer = stackalloc byte[8];
			NextBytes(buffer);
			return BitConverter.ToUInt64(buffer);
		}

		public long NextPInt64()
		{
			Span<byte> buffer = stackalloc byte[8];
			NextBytes(buffer);
			buffer[7] &= 0x7F;
			return BitConverter.ToInt64(buffer);
		}

		public int NextPositiveInt() { return random.Next(0, int.MaxValue); }
		public int NextNegativeInt() { return random.Next(int.MinValue, 0); }

		public int NextWithoutZero()
		{
			while (true)
			{
				int n = random.Next();
				if (n != 0) return n;
			}
		}

		public void NextBytes(byte[] buffer) { random.NextBytes(buffer); }

		public void NextBytes(Span<byte> buffer) { random.NextBytes(buffer); }

		public float NextFloat() { return (float)random.NextDouble(); }

		public float NextFloat(float min, float max)
		{
			float rnd = ((float)random.Next()) / (float)(int.MaxValue - 1);
			float diff = max - min;
			float offset = rnd * diff;
			return min + offset;
		}

		public double NextDouble() { return random.NextDouble(); }

		public double NextDouble(double min, double max)
		{
			double rnd = ((double)random.Next()) / (double)(int.MaxValue - 1);
			double diff = max - min;
			double offset = rnd * diff;
			return min + offset;
		}

		public string NextString(int length)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < length; i++)
				sb.Append(characters[Next(characters.Length)]);
			return sb.ToString();
		}

		public string NextStringUpper(int length)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < length; i++)
				sb.Append((char)Next('A', 'Z' + 1));
			return sb.ToString();
		}

		public string NextStringLower(int length)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < length; i++)
				sb.Append((char)Next('a', 'z' + 1));
			return sb.ToString();
		}

		public void Shuffle<T>(T[] array)
		{
			for (int i = array.Length - 1; i >= 0; i--)
			{
				int index = random.Next(i + 1);
				T tmp = array[i];
				array[i] = array[index];
				array[index] = tmp;
			}
		}

		public void Shuffle<T>(List<T> list)
		{
			for (int i = list.Count - 1; i >= 0; i--)
			{
				int index = random.Next(i + 1);
				T tmp = list[i];
				list[i] = list[index];
				list[index] = tmp;
			}
		}

		public bool Check(double ratio)
		{
			var rnd = random.NextDouble();
			return (rnd <= ratio);
		}
	}
}
