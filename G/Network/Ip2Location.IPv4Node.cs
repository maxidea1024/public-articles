using System.Collections.Generic;
using System.Net;

namespace G.Network
{
	public partial class Ip2Location
	{
		class IpNode
		{
			private int maskIndex;
			private Dictionary<byte, IpNode> dicChild = new Dictionary<byte, IpNode>();
			private List<IpGeo> listIPv4 = new List<IpGeo>();

			public IpNode(int maskIndex)
			{
				this.maskIndex = maskIndex;
			}

			public void Add(IpGeo ipGeo)
			{
				var maskBit = maskIndex * 8 + 8;

				if (ipGeo.Cidr.Mask > maskBit)
				{
					var ipByte = ipGeo.Cidr.GetIpByte(maskIndex);

					if (dicChild.TryGetValue(ipByte, out var ipNode))
					{
						ipNode.Add(ipGeo);
					}
					else
					{
						ipNode = new IpNode(maskIndex + 1);
						ipNode.Add(ipGeo);
						dicChild.Add(ipByte, ipNode);
					}
				}
				else
				{
					listIPv4.Add(ipGeo);
				}
			}

			public IpGeo Find(string ip)
			{
				var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
				return Find(ip);
			}

			public IpGeo Find(byte[] ip)
			{
				foreach (var i in listIPv4)
				{
					if (i.Cidr.Check(ip)) return i;
				}

				if (dicChild.TryGetValue(ip[maskIndex], out var child))
					return child.Find(ip);

				return null;
			}
		}
	}
}
