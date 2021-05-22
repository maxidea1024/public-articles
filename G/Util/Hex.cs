namespace G.Util
{
	public static class Hex
	{
		private static char[] hexes = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };
		
		public static string ToString(byte[] buffer)
		{
			char[] result = new char[buffer.Length << 1];
			
			int offset = 0;
			foreach (byte b in buffer)
			{
				result[offset++] = hexes[b >> 4];
				result[offset++] = hexes[b & 0x0F];
			}
			
			return new string(result);
		}
		
		public static byte[] FromString(string hex)
		{
			byte[] buffer = new byte[hex.Length / 2];
			int length = buffer.Length;
			
			int offset = 0;
			
			for (int i = 0; i < length; i++)
			{
				char hi = hex[offset++];
				char lo = hex[offset++];
				
				buffer[i] = (byte)(
					(
					(hi <= '9') ? hi - '0' :
					(hi >= 'a') ? hi - 'a' + 10 :
					hi - 'A' + 10
					) << 4 |
					(
					(lo <= '9') ? lo - '0' :
					(lo >= 'a') ? lo - 'a' + 10 :
					lo - 'A' + 10
					)
					);
			}
			
			return buffer;
		}
	}
}
