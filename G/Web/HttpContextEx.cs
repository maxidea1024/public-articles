using System;
using Microsoft.AspNetCore.Http;
using G.Network;

namespace G.Web
{
	public static class HttpContextEx
	{
        public static string GetRemoteIp(this HttpContext context)
        {
            var forward = context.Request.Headers["X-Forwarded-For"];
            if (forward.Count == 0)
                return context.Connection.RemoteIpAddress.ToString();
            else
                return forward[0];
        }

        public static (string RemoteIp, string Country) GetRemoteIpAndCountry(this HttpContext context)
        {
            string remoteIp = context.GetRemoteIp();
            string country = Ip2Location.FindCountryCode(remoteIp);

            if (string.IsNullOrWhiteSpace(country))
                country = "KR";
            else if (Ip2Location.IsBlockCountry(country))
                throw new Exception($"Blocked CountryIP : {remoteIp}");

            return (remoteIp, country);
        }
	}
}