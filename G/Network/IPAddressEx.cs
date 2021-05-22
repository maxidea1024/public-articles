using System;

namespace G.Network
{
	public class IPAddressEx
	{
		public static bool IsInternal(string ipAddress)
		{
			if (ipAddress.StartsWith("10.0.")) return true;
			if (ipAddress.StartsWith("192.168.0.")) return true;
			if (ipAddress.Equals("127.0.0.1")) return true;

			if (ipAddress.StartsWith("::ffff:10.0.")) return true;
			if (ipAddress.StartsWith("::ffff:192.168.0.")) return true;
			if (ipAddress.Equals("::ffff:127.0.0.1")) return true;

			if (ipAddress.Equals("::1")) return true;

			return false;
		}

		public static bool IsLocal(string ipAddress)
		{
			if (ipAddress.Equals("127.0.0.1")) return true;
			if (ipAddress.Equals("::ffff:127.0.0.1")) return true;
			if (ipAddress.Equals("::1")) return true;

			return false;
		}
	}
}
