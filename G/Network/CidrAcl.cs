using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using G.Util;

namespace G.Network
{
    public class CidrAcl
    {
        private List<Cidr> list = new List<Cidr>();

        public CidrAcl()
        {
        }

        public CidrAcl(string path)
        {
            string[] cidrs = File.ReadAllLines(path);
            Add(cidrs);
        }

        public CidrAcl(string[] cidrs)
        {
            Add(cidrs);
        }

        public void Clear()
        {
            list.Clear();
        }

        public void Add(string cidr)
        {
            if (cidr == null) return;
            
            cidr = cidr.Trim();
            if (cidr.Length == 0) return;
            if (cidr.StartsWith("#")) return;

            list.Add(new Cidr(cidr));
        }

        public void Add(string[] cidrs)
        {
            foreach (var c in cidrs)
                Add(c);
        }

        public bool Check(byte[] ip)
        {
            try
            {
                foreach (var c in list)
                {
                    if (c.Check(ip)) return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Check(IPAddress ip)
        {
            return Check(ip.GetAddressBytes());
        }

        public bool Check(string ip)
        {
            IPAddress ipAddress;
            if (IPAddress.TryParse(ip, out ipAddress))
                return Check(ipAddress.GetAddressBytes());
            else
                return false;
        }
    }
}
