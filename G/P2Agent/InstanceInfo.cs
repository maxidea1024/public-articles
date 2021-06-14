// us-east-1       미국동부(버지니아)
// us-east-2       미국동부(오하이오)
// us-west-1       미국서부(캘리포니아)
// us-west-2       미국서부(오레곤)
// af-south-1      아프리카(케이프타운)
// ap-east-1       아시아태평양(홍콩)
// ap-south-1      아시아태평양(뭄바이)
// ap-northeast-1  아시아태평양(도쿄)
// ap-northeast-2  아시아태평양(서울)
// ap-northeast-3  아시아태평양(오사카)
// ap-southeast-1  아시아태평양(싱가포르)
// ap-southeast-2  아시아태평양(시드니)
// ca-centra-1     캐나다(중부)
// eu-central-1    유럽(프랑크푸르트)
// eu-west-1       유럽(아일랜드)
// eu-west-2       유럽(런던)
// eu-west-3       유럽(파리)
// eu-south-1      유럽(밀라노)
// eu-north-1      유럽(스톡홀름)
// me-south-1      중동(바레인)
// sa-east-1       남아메리카(상파울루)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using NetworkInterface = System.Net.NetworkInformation.NetworkInterface;

namespace G.P2Agent
{
    public class InstanceInfo
    {
        public static readonly InstanceType UniversalInstanceType = new InstanceType("UNIVERSAL");

        public class SecurityGroup
        {
            public string Name { get; set; }
            public string Id { get; set; }
        }

        public static bool IsRunningOnEC2 => InstanceType != UniversalInstanceType;
        public static string ServiceRegion { get; private set; }
        public static string ServiceSwitch { get; private set; }
        public static string Region { get; private set; }
        public static string AZ { get; private set; }
        public static SecurityGroup[] SecurityGroups { get; private set; }
        public static InstanceType InstanceType { get; private set; }
        public static KeyValuePair<string,string>[] InstanceTags { get; private set; }
        public static string InstanceId { get; private set; }
        public static string InstanceName { get; private set; }
        public static string HostName { get; private set; }
        public static string MacAddress { get; private set; }
        public static string VpcId { get; set; }
        public static string SubnetId { get; set; }
        public static string[] LocalIPAddresses { get; private set; }
        public static string[] LocalIPAddressesV6 { get; private set; }
        public static string PublicIP { get; private set; }
        public static string PublicDnsName { get; private set; }
        public static string PrivateDnsName { get; private set; }

        public static TimeSpan ProcessUptime => (DateTime.Now - Process.GetCurrentProcess().StartTime); //Process.StartTime은 로컬 기준임.

        public static TimeSpan SystemUptime
        {
            get
            {
                var ticks = Stopwatch.GetTimestamp();
                var uptime = ((double)ticks) / Stopwatch.Frequency;
                return TimeSpan.FromSeconds(uptime);
            }
        }

        //todo 외부에서 가져올수 있도록 해야함.
        static readonly string AwsAccessKeyId = "AKIAVOUCEKPUY5VNRQWP";
        static readonly string AwsSecretAccessKey = "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";
        //static readonly string AwsAccountId = "375008809961";
        //static readonly string AwsKeyPairName = "haegindev1";

        static string GroupNames(List<GroupIdentifier> groups)
        {
            string result = "";
            foreach (var group in groups)
            {
                if (result.Length > 0)
                    result += ",";
                result += $"{group.GroupId}({group.GroupName})";
            }
            return result;
        }

        static string GetTagValueFromTags(List<Tag> tags, string key, string defaultName = "")
        {
            foreach (var tag in tags)
            {
                if (tag.Key == key)
                {
                    return tag.Value;
                }
            }

            return defaultName;
        }

        public static async Task InitializeAsync()
        {
            InstanceType = UniversalInstanceType;
            InstanceId = "";
            InstanceName = "";
            InstanceTags = Array.Empty<KeyValuePair<string,string>>();

            HostName = Dns.GetHostName();
            SecurityGroups = Array.Empty<SecurityGroup>();
            AZ = "";
            Region = "";
            ServiceRegion = "";
            ServiceSwitch = "";

            VpcId = "";
            SubnetId = "";

            MacAddress = GetMacAddress();

            PublicDnsName = "";
            PrivateDnsName = "";

            // 우선은 az를 구해야함.
            var az = await GetEC2MetadataAsync("http://169.254.169.254/latest/meta-data/placement/availability-zone");

            if (!string.IsNullOrEmpty(az))
            {
                //remove trailing a/b
                string region = az.Substring(0, az.Length - 1);

                var regionEndPoint = RegionEndpoint.GetBySystemName(region);

                using var ec2Client = new AmazonEC2Client(AwsAccessKeyId, AwsSecretAccessKey, regionEndPoint);
                var instanceId = await GetEC2MetadataAsync("http://169.254.169.254/latest/meta-data/instance-id");

                Instance instance = null;

                //이렇게 조회하는게 아닌가?
                //첫번째 인스턴스로 고정이되네
                //어떻게 찾아야하는거지?
                //찾아서?
                //ID를 하나 넣었는데 무조건 검색되나?
                //이부분을 어떻게 해결해야할까?
                var request = new DescribeInstancesRequest();
                request.InstanceIds.Add(instanceId); //이거를 넣었는데 왜 첫번째인데 이게 안나오지? 그냥 전체 목록이 나오나?
                var response = await ec2Client.DescribeInstancesAsync();
                if (response.Reservations.Count > 0)
                {
                    //Console.WriteLine($"RegionEndPoint: {regionEndPoint}");
                    //Console.WriteLine($"ThisInstanceId: {instanceId}");
                    //Console.WriteLine($"ReservationCount: {response.Reservations.Count}");

                    if (response.Reservations.Count > 0)
                    {
                        foreach (var r in response.Reservations)
                        {
                            //Console.WriteLine($"Reservation: {r.ReservationId}");

                            foreach (var i in r.Instances)
                            {
                                //Console.WriteLine($"  >> Instance: {i.InstanceId}");

                                if (i.InstanceId == instanceId)
                                {
                                    instance = i;
                                    break;
                                }
                            }

                            if (instance != null)
                                break;
                        }

                        //instance = response.Reservations[0].Instances[0];
                    }
                }

                if (instance != null)
                {
                    InstanceId = instanceId;
                    InstanceType = instance.InstanceType;
                    InstanceName = GetTagValueFromTags(instance.Tags, "Name");
                    InstanceTags = instance.Tags.Select(x => new KeyValuePair<string,string>(x.Key, x.Value)).ToArray();
                    HostName = await GetEC2MetadataAsync("http://169.254.169.254/latest/meta-data/hostname"); //instance에서 가져올 수 없는건가?
                    SecurityGroups = instance.SecurityGroups.Select(x => new SecurityGroup { Id = x.GroupId, Name = x.GroupName}).ToArray();
                    ServiceRegion = GetTagValueFromTags(instance.Tags, "ServiceRegion");
					ServiceSwitch = GetTagValueFromTags(instance.Tags, "ServiceSwitch");
                    Region = region;
                    AZ = instance.Placement.AvailabilityZone;
                    MacAddress = instance.NetworkInterfaces[0].MacAddress;
                    VpcId = instance.VpcId ?? "";
                    SubnetId = instance.SubnetId ?? "";
                    PublicDnsName = instance.PublicDnsName ?? "";
                    PrivateDnsName = instance.PrivateDnsName ?? "";
                }
            }

            InstanceId ??= GetMachineId();

            LocalIPAddresses = GetLocalIPAddresses(AddressFamily.InterNetwork).ToArray();
            LocalIPAddressesV6 = GetLocalIPAddresses(AddressFamily.InterNetworkV6).ToArray();
            PublicIP = await GetPublicIPAddressAsync();
        }

        private static async Task<string> GetEC2MetadataAsync(string url)
        {
            try
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2)
                };

                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var stringResponse = await response.Content.ReadAsStringAsync();
                    return stringResponse;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static string GetMachineId()
        {
            string recipes = "";
            recipes += Environment.MachineName;
            recipes += Environment.UserName;
            recipes += Environment.UserDomainName;
            recipes += GetMacAddress();

            var bytes = Encoding.UTF8.GetBytes(recipes);
            byte[] hash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                md5.TransformFinalBlock(bytes, 0, bytes.Length);
                hash = md5.Hash;
            }

            var hex = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                hex.AppendFormat("{0:x2}", hash[i]);
            }
            return hex.ToString();
        }

        private static string GetMacAddress()
        {
            string macAddresses = string.Empty;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    macAddresses += nic.GetPhysicalAddress().ToString();
                    break;
                }
            }

            return macAddresses;
        }

        private static List<string> GetLocalIPAddresses(AddressFamily addressFamily = AddressFamily.InterNetwork)
        {
            var results = new List<string>();

            NetworkInterface.GetAllNetworkInterfaces().ToList().ForEach(ni =>
            {
                if (ni.GetIPProperties().GatewayAddresses.FirstOrDefault() != null)
                {
                    ni.GetIPProperties().UnicastAddresses.ToList().ForEach(ua =>
                    {
                        if (ua.Address.AddressFamily == addressFamily)
                        {
                            results.Add(ua.Address.ToString());
                        }
                    });
                }
            });

            return results.Distinct().ToList();
        }

        public static async Task<string> GetPublicIPAddressAsync()
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
