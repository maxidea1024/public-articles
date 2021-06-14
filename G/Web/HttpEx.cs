using System;
using Microsoft.AspNetCore.Http;

namespace G.Web
{
	public class HttpEx
	{
		public static string GetRemoteIp(HttpContext context)
		{
			var forward = context.Request.Headers["X-Forwarded-For"];
			if (forward.Count == 0)
				return context.Connection.RemoteIpAddress.ToString();
			else
				return forward[0];
		}
	}
}