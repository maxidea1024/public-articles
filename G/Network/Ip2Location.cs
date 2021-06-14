using System.Collections.Generic;
using System.IO;
using System.Net;
using G.Util;

namespace G.Network
{
    public partial class Ip2Location
    {
        public class IpGeo
        {
            public Cidr Cidr { get; set; }
            public int GeoNameId { get; set; }
        }

        public class Location
        {
            public int GeoNameId { get; set; }
            public string Locale { get; set; }
            public string Continent { get; set; }
            public string CountryCode { get; set; }
            public string CountryName { get; set; }
            public bool IsEU { get; set; }
        }

        private static IpNode ipv4Root;
        private static IpNode ipv6Root;
        private static Dictionary<int, Location> dicLocation = new Dictionary<int, Location>();

        public static void Initialize()
        {
            ReadIPv4s(Ip2LocationConfig.Singleton.IPv4Path);
            ReadIPv6s(Ip2LocationConfig.Singleton.IPv6Path);
            ReadLocations(Ip2LocationConfig.Singleton.LocationPath);
        }

        public static void ReadIPv4s(string path)
        {
            path = FileEx.SearchParentDirectory(path);

            ipv4Root = new IpNode(0);

            bool isFirst = true;
            foreach (var line in File.ReadLines(path))
            {
                if (isFirst)
                {
                    isFirst = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                var tokens = line.Split(',');
                if (tokens.Length < 2) continue;
                if (string.IsNullOrEmpty(tokens[1])) continue;

                var ipGeo = new IpGeo
                {
                    Cidr = new Cidr(tokens[0]),
                    GeoNameId = int.Parse(tokens[1])
                };

                ipv4Root.Add(ipGeo);
            }
        }

        public static void ReadIPv6s(string path)
        {
            path = FileEx.SearchParentDirectory(path);

            ipv6Root = new IpNode(0);

            bool isFirst = true;
            foreach (var line in File.ReadLines(path))
            {
                if (isFirst)
                {
                    isFirst = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                var tokens = line.Split(',');
                if (tokens.Length < 2) continue;
                if (string.IsNullOrEmpty(tokens[1])) continue;

                var ipGeo = new IpGeo
                {
                    Cidr = new Cidr(tokens[0]),
                    GeoNameId = int.Parse(tokens[1])
                };

                ipv6Root.Add(ipGeo);
            }
        }

        public static void ReadLocations(string path)
        {
            path = FileEx.SearchParentDirectory(path);

            dicLocation.Clear();

            bool isFirst = true;
            foreach (var line in File.ReadLines(path))
            {
                if (isFirst)
                {
                    isFirst = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                var tokens = line.Split(',');
                if (tokens.Length < 5) continue;

                var location = new Location
                {
                    GeoNameId = int.Parse(tokens[0]),
                    Locale = tokens[1],
                    Continent = tokens[2],
                    CountryCode = tokens[4],
                    CountryName = tokens[5],
                    IsEU = ConvertEx.ToBoolean(tokens[6])
                };

                dicLocation[location.GeoNameId] = location;
            }
        }

        public static int FindGeoNameId(string ip)
        {
            var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
            return FindGeoNameId(ipBytes);
        }

        public static int FindGeoNameId(byte[] ip)
        {
            IpNode ipRoot = null;
            if (ip.Length == 4)
                ipRoot = ipv4Root;
            else if (ip.Length == 16)
                ipRoot = ipv6Root;

            if (ipRoot == null) return -1;

            var ipGeo = ipRoot.Find(ip);
            if (ipGeo == null) return -1;

            return ipGeo.GeoNameId;
        }

        public static Location Find(string ip)
        {
            var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
            return Find(ipBytes);
        }

        public static Location Find(byte[] ip)
        {
            var geoNameId = FindGeoNameId(ip);

            if (dicLocation.TryGetValue(geoNameId, out var location))
                return location;
            else
                return null;
        }

        public static string FindCountryCode(string ip)
        {
            //if (ip.StartsWith("10.")) return null;
            //if (ip.StartsWith("192.168.")) return null;
            //if (ip.StartsWith("127.0.0.1")) return null;

            var ipAddress = IPAddress.Parse(ip);
            if (ipAddress.IsIPv4MappedToIPv6)
            {
                ip = ipAddress.MapToIPv4().ToString();
            }

            if (ip.StartsWith("10.")) return "0A";
            if (ip.StartsWith("192.168.")) return "C0";
            if (ip.StartsWith("127.0.0.1")) return "7F";

            var location = Find(ip);
            if (location == null) return null;

            return location.CountryCode;
        }

        #region Block Country IP
        public static HashSet<string> BlockCountries { get; private set; } = new HashSet<string>();

        public static void SetBlockContries(string countries)
        {
            var blockCountries = new HashSet<string>();
            if (string.IsNullOrEmpty(countries) == false)
            {
                var tokens = countries.Split(',');
                foreach (var t in tokens)
                {
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    blockCountries.Add(t.Trim());
                }
            }
            BlockCountries = blockCountries;
        }

        public static bool IsBlockCountry(string country)
        {
            if (country == null) return false;
            return BlockCountries.Contains(country);
        }

        public static bool IsBlockCountryIp(string ip)
        {
            var country = FindCountryCode(ip);
            return IsBlockCountry(country);
        }
        #endregion
    }
}
