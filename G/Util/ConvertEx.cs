using System;
using System.Collections.Generic;

namespace G.Util
{
    public class ConvertEx
    {
        public static uint[] ToKey(string base62)
        {
            byte[] keyBytes = Base62.FromBase62(base62);
            if (keyBytes.Length < 16) throw new Exception("Wrong Key Length");

            uint[] key = new uint[4];
            key[0] = BitConverter.ToUInt32(keyBytes, 0);
            key[1] = BitConverter.ToUInt32(keyBytes, 4);
            key[2] = BitConverter.ToUInt32(keyBytes, 8);
            key[3] = BitConverter.ToUInt32(keyBytes, 12);

            return key;
        }

        public static string ToBase62(uint[] key)
        {
            if (key.Length != 4) throw new Exception("Wrong Key Length");

            byte[] buffer = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(key[0]), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(key[1]), 0, buffer, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(key[2]), 0, buffer, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(key[3]), 0, buffer, 12, 4);

            return Base62.ToBase62(buffer);
        }

        public static ushort[] ToVersion(string version)
        {
            ushort[] ver = new ushort[4];
            List<ushort> list = StringEx.FromStringWithDot<ushort>(version);

            for (int i = 0; i < 4; i++)
            {
                if (i >= list.Count) break;
                ver[i] = list[i];
            }

            return ver;
        }

        public static string FromVersion(params ushort[] ver)
        {
            return StringEx.ToStringWithDot(ver);
        }

        public static bool ToBoolean(object obj)
        {
            if (obj == null) return false;

            if (obj is int)
            {
                return ((int) obj != 0);
            }

            if (obj is string)
            {
                string value = obj.ToString().ToLower();
                if (value == "true") return true;
                if (value == "yes") return true;
                if (value == "1") return true;
                return false;
            }

            if (obj is bool)
            {
                return (bool) obj;
            }

            return false;
        }
    }
}
