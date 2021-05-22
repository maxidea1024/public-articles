using System.IO;
using System.Text;

namespace G.Util
{
    public static class Base62
    {
        private static string codes = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public static string ToBase62(this byte[] data)
        {
            var sb = new StringBuilder();
            using (var stream = new BitStream(data))
            {
                byte[] buffer = new byte[1];
                while (true)
                {
                    buffer[0] = 0;
                    int length = stream.Read(buffer, 0, 6);
                    if (length == 6)
                    {
                        if ((int) (buffer[0] >> 3) == 0x1f)
                        {
                            sb.Append(codes[61]);
                            stream.Seek(-1, SeekOrigin.Current);
                        }
                        else if ((int) (buffer[0] >> 3) == 0x1e)
                        {
                            sb.Append(codes[60]);
                            stream.Seek(-1, SeekOrigin.Current);
                        }
                        else
                        {
                            sb.Append(codes[(int) (buffer[0] >> 2)]);
                        }
                    }
                    else if (length == 0)
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(codes[(int) (buffer[0] >> (int) (8 - length))]);
                        break;
                    }
                }

                return sb.ToString();
            }
        }

        public static byte[] FromBase62(this string text)
        {
            int count = 0;

            using (var stream = new BitStream(text.Length * 6 / 8))
            {
                foreach (char c in text)
                {
                    int index = codes.IndexOf(c);

                    if (count == text.Length - 1)
                    {
                        int mod = (int) (stream.Position % 8);
                        if (mod == 0)
                            throw new InvalidDataException("An extra character was found");

                        if ((index >> (8 - mod)) > 0)
                            throw new InvalidDataException("Invalid ending character was found");

                        stream.Write(new byte[] {(byte) (index << mod)}, 0, 8 - mod);
                    }
                    else
                    {
                        if (index == 60)
                        {
                            stream.Write(new byte[] {0xf0}, 0, 5);
                        }
                        else if (index == 61)
                        {
                            stream.Write(new byte[] {0xf8}, 0, 5);
                        }
                        else
                        {
                            stream.Write(new byte[] {(byte) index}, 2, 6);
                        }
                    }

                    count++;
                }

                byte[] result = new byte[stream.Position / 8];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(result, 0, result.Length * 8);

                return result;
            }
        }
    }
}
