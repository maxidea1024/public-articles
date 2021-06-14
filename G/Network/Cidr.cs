using System.Net;

namespace G.Network
{
	public class Cidr
	{
		private byte[] ipBytes;
		private int byteCount;
		private int bitCount;

		public IPAddress IP { get; private set; }
		public int Mask { get; private set; }

		public Cidr(IPAddress ip, int mask)
		{
			Set(ip, mask);
		}

		public Cidr(IPAddress ip)
		{
			Set(ip, ip.GetAddressBytes().Length * 8);
		}

		public Cidr(string ip, int mask)
		{
			Set(IPAddress.Parse(ip), mask);
		}

		public Cidr(string ipAndMask)
		{
			string[] tokens = ipAndMask.Split('/');

			var ip = IPAddress.Parse(tokens[0]);

			if (tokens.Length > 1)
				Set(ip, int.Parse(tokens[1]));
			else
				Set(ip, ip.GetAddressBytes().Length * 8);
		}

		public Cidr(byte[] ip, int mask)
		{
			Set(new IPAddress(ip), mask);
		}

		public Cidr(byte[] ip)
		{
			Set(new IPAddress(ip), ipBytes.Length * 8);
		}

		private void Set(IPAddress ip, int mask)
		{
			ipBytes = ip.GetAddressBytes();
			Mask = mask;
			byteCount = mask / 8;
			bitCount = mask % 8;

			if (bitCount > 0)
			{
				byte b = ipBytes[byteCount];
				switch (bitCount)
				{
					case 1: b &= 0x80; break;
					case 2: b &= 0xC0; break;
					case 3: b &= 0xE0; break;
					case 4: b &= 0xF0; break;
					case 5: b &= 0xF8; break;
					case 6: b &= 0xFC; break;
					case 7: b &= 0xFE; break;
				}
				ipBytes[byteCount] = b;
			}

			int zeroCount = ipBytes.Length - (mask + 7) / 8;
			for (int i = 1; i <= zeroCount; i++)
			{
				ipBytes[ipBytes.Length - i] = 0;
			}

			IP = new IPAddress(ipBytes);
		}

		public byte GetIpByte(int index)
		{
			if (ipBytes == null || ipBytes.Length <= index)
				return 0;
			else
				return ipBytes[index];
		}

		public override string ToString()
		{
			return string.Format("{0}/{1}", IP, Mask);
		}

		public bool Check(byte[] ip)
		{
			int length = ip.Length;
			if (length != ipBytes.Length) return false;

			int i = 0;
			for (; i < byteCount; i++)
			{
				if (ip[i] != ipBytes[i]) return false;
			}

			switch (bitCount)
			{
				case 1: return (ip[i] & 0x80) == ipBytes[i];
				case 2: return (ip[i] & 0xC0) == ipBytes[i];
				case 3: return (ip[i] & 0xE0) == ipBytes[i];
				case 4: return (ip[i] & 0xF0) == ipBytes[i];
				case 5: return (ip[i] & 0xF8) == ipBytes[i];
				case 6: return (ip[i] & 0xFC) == ipBytes[i];
				case 7: return (ip[i] & 0xFE) == ipBytes[i];
			}

			return true;
		}

		public bool Check(IPAddress ip)
		{
			return Check(ip.GetAddressBytes());
		}

		public bool Check(string ip)
		{
			IPAddress ipAddr;
			if (IPAddress.TryParse(ip, out ipAddr))
				return Check(ipAddr);
			return false;
		}
	}
}
