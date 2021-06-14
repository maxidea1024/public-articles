using G.Util;

namespace G.Network
{
	public class Ip2LocationConfig
	{
		public string IPv4Path { get; set; }
		public string IPv6Path { get; set; }
		public string LocationPath { get; set; }

		public static Ip2LocationConfig Singleton { get; private set; }

		static Ip2LocationConfig()
		{
			Load("Ip2Location.config");
		}

		public static void Load(string path)
		{
			Singleton = JsonAppConfig.Load<Ip2LocationConfig>(path);
		}
	}
}
