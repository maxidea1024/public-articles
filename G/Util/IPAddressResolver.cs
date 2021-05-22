using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace G.Util
{
    public class IPAddressResolver
    {
        /// <summary>
        /// 로컬 IP 어드레스 목록을 가져옴.
        /// </summary>
        /// <param name="addressFamily"></param>
        /// <returns></returns>
        public static List<string> GetLocalIPAddresses(AddressFamily addressFamily = AddressFamily.InterNetwork)
        {
            var founds = new List<string>();

            NetworkInterface.GetAllNetworkInterfaces().ToList().ForEach(ni =>
            {
                if (ni.GetIPProperties().GatewayAddresses.FirstOrDefault() != null)
                {
                    ni.GetIPProperties().UnicastAddresses.ToList().ForEach(ua =>
                    {
                        if (ua.Address.AddressFamily == addressFamily)
                        {
                            founds.Add(ua.Address.ToString());
                        }
                    });
                }
            });

            return founds.Distinct().ToList();
        }

        /// <summary>
        /// External(Public) IP 어드레스 조회.
        /// </summary>
        /// <returns></returns>
        public static string GetExternalIPAddress()
        {
            return GetExternalIPAddressAsync().Result;
        }

        /// <summary>
        /// External(Public) IP 어드레스 조회.
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetExternalIPAddressAsync()
        {
            string result = string.Empty;

            string[] checkIPUrl =
            {
                "https://ipinfo.io/ip",
                "https://checkip.amazonaws.com/",
                "https://api.ipify.org",
                "https://icanhazip.com",
                "https://wtfismyip.com/text"
            };

            using (var client = new WebClient())
            {
                client.Headers["User-Agent"] = "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 6.0) (compatible; MSIE 6.0; Windows NT 5.1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";

                foreach (var url in checkIPUrl)
                {
                    try
                    {
                        result = await client.DownloadStringTaskAsync(url);
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(result))
                    {
                        break;
                    }
                }
            }

            return result.Replace("\n", "").Trim();
        }
    }
}
