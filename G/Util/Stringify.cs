using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace G.Util
{
    /// <summary>
    /// warning: This helper class has limited use and should not be used outside for general use!
    /// </summary>
    public static class Stringify
    {
        [Flags]
        public enum Closing
        {
            Closed = BeginClosed | EndClosed,
            BeginClosed = 0x01,
            EndClosed = 0x02
        }

        [ThreadStatic]
        private static Closing _currentClosing;

        public static StringBuilder Begin(Closing closing = Closing.Closed)
        {
            _currentClosing = closing;

            var sb = new StringBuilder();
            if ((closing & Closing.BeginClosed) != 0)
            {
                sb.Append("{");
            }
            return sb;
        }

        public static string End(StringBuilder output)
        {
            if ((_currentClosing & Closing.EndClosed) != 0)
            {
                output.Append("}");
            }

            return output.ToString();
        }

        public static void Append(StringBuilder output, string key, object value, bool hex = false)
        {
            if (output.Length > 1) // '{'
            {
                output.Append(",");
            }

            output.Append("\"");
            output.Append(key);
            output.Append("\":");
            AppendValue(output, value, hex);
        }

        static void AppendValue(StringBuilder output, object value, bool hex = false)
        {
            if (value == null)
            {
                output.Append("null");
                return;
            }

            Type type = value.GetType();
            if (type == typeof(string) || type == typeof(char))
            {
                output.Append('"');
                string str = value.ToString();
                for (int i = 0; i < str.Length; ++i)
                {
                    if (str[i] < ' ' || str[i] == '"' || str[i] == '\\')
                    {
                        output.Append('\\');
                        int j = "\"\\\n\r\t\b\f".IndexOf(str[i]);
                        if (j >= 0)
                        {
                            output.Append("\"\\nrtbf"[j]);
                        }
                        else
                        {
                            output.AppendFormat("u{0:X4}", (UInt32)str[i]);
                        }
                    }
                    else
                    {
                        output.Append(str[i]);
                    }
                }
                output.Append('"');
            }
            else if (type == typeof(byte) || type == typeof(sbyte))
            {
                output.Append(value.ToString());
            }
            else if (type == typeof(short) || type == typeof(ushort))
            {
                output.Append(value.ToString());
            }
            else if (type == typeof(int) || type == typeof(uint))
            {
                output.Append(value.ToString());
            }
            else if (type == typeof(long) || type == typeof(ulong))
            {
                output.Append(value.ToString());
            }
            else if (type == typeof(float))
            {
                output.Append(((float)value).ToString(CultureInfo.InvariantCulture));
            }
            else if (type == typeof(double))
            {
                output.Append(((double)value).ToString(CultureInfo.InvariantCulture));
            }
            else if (type == typeof(decimal))
            {
                output.Append(((decimal)value).ToString(CultureInfo.InvariantCulture));
            }
            else if (type == typeof(bool))
            {
                output.Append(((bool)value) ? "true" : "false");
            }
            else if (type.IsEnum)
            {
                // 단순 숫자일 경우에는 quote를 씌우지 않는것도 좋을듯 싶다.
                var str = value.ToString();
                if (str.Length > 0 && char.IsDigit(str[0]))
                {
                    output.Append(str);
                }
                else
                {
                    output.Append('"');
                    output.Append(str);
                    output.Append('"');
                }
            }
            else if (type == typeof(byte[]))
            {
                output.Append("\"");
                output.Append(Convert.ToBase64String((byte[])value)); //todo 길이 제한을 하는게 좋지 않을까?
                output.Append("\"");
            }
            else if (value is IEnumerable enumerable)
            {
                output.Append('[');
                bool isFirst = true;
                foreach (var item in enumerable)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        output.Append(",");
                    }
                    AppendValue(output, item);
                }
                output.Append(']');
            }
            else if (value is IList list)
            {
                output.Append('[');
                bool isFirst = true;
                for (int i = 0; i < list.Count; i++)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        output.Append(",");
                    }
                    AppendValue(output, list[i]);
                }
                output.Append(']');
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type keyType = type.GetGenericArguments()[0];

                // Refuse to output dictionary keys that aren't of type string
                if (keyType != typeof(string))
                {
                    output.Append("{}");
                    return;
                }

                output.Append('{');
                IDictionary dict = value as IDictionary;
                bool isFirst = true;
                foreach (object key in dict.Keys)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        output.Append(",");
                    }
                    output.Append('\"');
                    output.Append((string)key); // lower camel cased로 바꿔주는게 좋을듯?
                    output.Append("\":");
                    AppendValue(output, dict[key]);
                }
                output.Append('}');
            }
            else if (type == typeof(Guid))
            {
                output.Append("\"");
                output.Append(value.ToString());
                output.Append("\"");
            }
            else if (type == typeof(TimeSpan))
            {
                var span = (TimeSpan)value;
                output.Append("\"");
                output.Append(span.ToString(@"d\.hh\:mm\:ss\:FFF"));
                output.Append("\"");
            }
            else if (type == typeof(DateTime))
            {
                var datetime = (DateTime)value;
                output.Append("\"");
                output.Append(datetime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                output.Append("\"");
            }
            else if (type == typeof(DateTimeOffset))
            {
                var offset = (DateTimeOffset)value;
                output.Append("\"");
                output.Append(offset.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                output.Append("\"");
            }
            else
            {
                //if (!type.IsAssignableFrom(typeof(IGeneratedStruct)))
                //{
                //    throw new NotSupportedException($"The Stringfy class is a simple class intended to be used for a limited purpose.Type {type.FullName} is not supported.");
                //}
                //else
                {
                    //output.Append(value.ToString());
                    output.Append("\"");
                    output.Append(value.ToString());
                    output.Append("\"");

                    // 그냥 string 타입으로 변환해서 재귀적으로 처리(일반적인 상황인지?)
                    // 이렇게 하면 " 문자가 escaping되어서 \" 로 바뀌는 문제가 있다.
                    // Recursive for makes to quoted string value
                    //AppendValue(output, value.ToString());
                }
            }
        }
    }
}
