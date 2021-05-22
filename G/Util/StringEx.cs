using System;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace G.Util
{
    public static class StringEx
    {
        public static string LineSingle(int length, string title = null)
        {
            return LineText('-', length, title);
        }

        public static string LineDouble(int length, string title = null)
        {
            return LineText('=', length, title);
        }

        public static string LineText(char ch, int length, string title = null)
        {
            if (String.IsNullOrEmpty(title))
                return new String(ch, length);

            int len = title.Length;
            if (len + 1 >= length)
                return title;
            else
                return String.Format("{0} {1}", title, new String(ch, length - len - 1));
        }

        public static string ToStringWithSeparator<T>(char separator, IEnumerable<T> enums)
        {
            if (enums == null) return string.Empty;

            bool isFirst = true;
            StringBuilder sb = new StringBuilder();
            foreach (T t in enums)
            {
                if (isFirst)
                    isFirst = false;
                else
                    sb.Append(separator);
                sb.Append(t);
            }

            return sb.ToString();
        }

        public static List<T> FromStringWithSeparator<T>(char separator, string text, T defaultValue = default(T))
        {
            string[] tokens = text.Split(separator);
            List<T> list = new List<T>(tokens.Length);
            foreach (string token in tokens)
            {
                if (String.IsNullOrWhiteSpace(token))
                    list.Add(defaultValue);
                else
                    list.Add((T) Convert.ChangeType(token, typeof(T)));
            }

            return list;
        }

        public static HashSet<T> FromStringWithSeparatorToHashSet<T>(char separator, string text,
            T defaultValue = default(T))
        {
            var set = new HashSet<T>();
            if (text.Length <= 0) return set;

            string[] tokens = text.Split(separator);
            foreach (string token in tokens)
            {
                if (String.IsNullOrWhiteSpace(token))
                    set.Add(defaultValue);
                else
                    set.Add((T) Convert.ChangeType(token, typeof(T)));
            }

            return set;
        }

        public static SortedSet<T> FromStringWithSeparatorToSortedSet<T>(char separator, string text,
            T defaultValue = default(T))
        {
            var set = new SortedSet<T>();
            if (text.Length <= 0) return set;

            string[] tokens = text.Split(separator);
            foreach (string token in tokens)
            {
                if (String.IsNullOrWhiteSpace(token))
                    set.Add(defaultValue);
                else
                    set.Add((T) Convert.ChangeType(token, typeof(T)));
            }

            return set;
        }

        public static string ToStringWithComma<T>(IEnumerable<T> enums)
        {
            return ToStringWithSeparator(',', enums);
        }

        public static List<T> FromStringWithComma<T>(string text, T defaultValue = default(T))
        {
            return FromStringWithSeparator<T>(',', text, defaultValue);
        }

        public static HashSet<T> FromStringWithCommaToHashSet<T>(string text, T defaultValue = default(T))
        {
            return FromStringWithSeparatorToHashSet<T>(',', text, defaultValue);
        }

        public static SortedSet<T> FromStringWithCommaToSortedSet<T>(string text, T defaultValue = default(T))
        {
            return FromStringWithSeparatorToSortedSet<T>(',', text, defaultValue);
        }

        public static string ToStringWithDot<T>(IEnumerable<T> enums)
        {
            return ToStringWithSeparator('.', enums);
        }

        public static List<T> FromStringWithDot<T>(string text, T defaultValue = default(T))
        {
            return FromStringWithSeparator<T>('.', text, defaultValue);
        }

        public static List<string[]> ConvertStringArrayList<T>(List<T> list)
        {
            List<string[]> stringArrayList = new List<string[]>();

            foreach (var p in list)
            {
                var count = p.GetType().GetProperties().Count();
                string[] values = new string[count];

                for (int i = 0; i < count; i++)
                {
                    var property = p.GetType().GetProperties()[i];
                    if (property.GetValue(p) == null)
                        values[i] = "";
                    else
                    {
                        if (property.PropertyType == typeof(DateTime))
                        {
                            var date = Convert.ToDateTime(property.GetValue(p));
                            values[i] = date.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            values[i] = (property.GetValue(p).ToString());
                        }
                    }
                }

                stringArrayList.Add(values);
            }

            return stringArrayList;
        }

        public static List<string[]> ConvertStringArrayList<T>(T[] list)
        {
            List<string[]> stringArrayList = new List<string[]>();

            foreach (var p in list)
            {
                var count = p.GetType().GetProperties().Count();
                string[] values = new string[count];

                for (int i = 0; i < count; i++)
                {
                    var property = p.GetType().GetProperties()[i];
                    if (property.GetValue(p) == null)
                        values[i] = "";
                    else
                    {
                        if (property.PropertyType == typeof(DateTime))
                        {
                            var date = Convert.ToDateTime(property.GetValue(p));
                            values[i] = date.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            values[i] = (property.GetValue(p).ToString());
                        }
                    }
                }

                stringArrayList.Add(values);
            }

            return stringArrayList;
        }

        #region Camel & Pascal

        public static string ToCamelString(string text)
        {
            return ToCamelPascalString(text, false);
        }

        public static string ToPascalString(string text)
        {
            return ToCamelPascalString(text, true);
        }

        public static string ToCamelPascalString(string text, bool isPascal = false)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = ArrageCase(text);

            StringBuilder sb = new StringBuilder();

            bool isFirst = true;
            string[] tokens = SplitToCamelPascal(text);
            foreach (var t in tokens)
            {
                string lt = t.ToLower();

                if (isFirst)
                {
                    isFirst = false;
                    if (isPascal)
                        sb.Append(ToUpperFirstCharacter(lt));
                    else
                        sb.Append(ToLowerFirstCharacter(lt));
                }
                else
                    sb.Append(ToUpperFirstCharacter(lt));
            }

            return sb.ToString();
        }

        public static string ToLowerFirstCharacter(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (!char.IsUpper(text[0])) return text;

            char[] chs = text.ToCharArray();
            chs[0] = char.ToLower(chs[0]);
            return new string(chs);
        }

        public static string ToUpperFirstCharacter(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (!char.IsLower(text[0])) return text;

            char[] chs = text.ToCharArray();
            chs[0] = char.ToUpper(chs[0]);
            return new string(chs);
        }

        public static string[] SplitToCamelPascal(string text)
        {
            if (text == null) return null;
            if (text.Length == 0) return new string[] {""};

            List<string> result = new List<string>();

            string[] tokens = text.Split('_', '-', ' ', '\t');
            foreach (var t in tokens)
            {
                string[] ss = _SplitToCamelPascal(t);
                foreach (var s in ss)
                {
                    if (string.IsNullOrEmpty(s.Trim())) continue;
                    result.Add(s);
                }
            }

            return result.ToArray();
        }

        private static string[] _SplitToCamelPascal(string text)
        {
            if (text == null) return null;
            if (text.Length == 0) return new string[] {""};

            List<string> tokens = new List<string>();

            int kind = 0; // 0:Start, 1:Upper, 2:Lower, 3:Etc
            int offset = 0;

            char[] chs = text.ToCharArray();
            for (int i = 0; i < chs.Length; i++)
            {
                char ch = chs[i];

                if (char.IsUpper(ch))
                {
                    if (kind == 0) kind = 1;
                    else if (kind != 1)
                    {
                        kind = 1;
                        tokens.Add(text.Substring(offset, i - offset));
                        offset = i;
                    }
                }
                else if (char.IsLower(ch))
                {
                    if (kind == 0 || kind == 1) kind = 2;
                    else if (kind != 2)
                    {
                        kind = 2;
                        tokens.Add(text.Substring(offset, i - offset));
                        offset = i;
                    }
                }
                else
                {
                    if (kind == 0) kind = 3;
                    else if (kind != 3)
                    {
                        kind = 3;
                        tokens.Add(text.Substring(offset, i - offset));
                        offset = i;
                    }
                }
            }

            if (chs.Length != offset)
            {
                tokens.Add(text.Substring(offset, chs.Length - offset));
            }

            return tokens.ToArray();
        }

        public static string ArrageCase(string text)
        {
            text = text.Replace("ID", "Id").Replace("SN", "Sn").Replace("NO", "No").Replace("LV", "Lv");
            text = text.Replace("MIN", "Min").Replace("MAX", "Max").Replace("HP", "Hp").Replace("MP", "Mp");
            text = text.Replace("BGM", "Bgm").Replace("EXP", "Exp").Replace("NAME", "Name");
            return text;
        }

        #endregion

        public static int CountOf(this string source, string text)
        {
            return source.CountOf(0, source.Length, text);
        }

        public static int CountOf(this string source, int offset, int length, string text)
        {
            int count = 0;

            while (true)
            {
                var n = source.IndexOf(text, offset, length - offset);
                if (n < 0) break;

                count++;
                offset = n + text.Length;
            }

            return count;
        }

        public static int[] IndexesOf(this string source, string text)
        {
            return source.IndexesOf(0, source.Length, text);
        }

        public static int[] IndexesOf(this string source, int offset, int length, string text)
        {
            var indexes = new List<int>();

            while (true)
            {
                var n = source.IndexOf(text, offset, length - offset);
                if (n < 0) break;

                indexes.Add(n);

                offset = n + text.Length;
            }

            return indexes.ToArray();
        }

        public static string RemoveComment(this string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            var indexes = text.IndexesOf("//");
            foreach (var i in indexes)
            {
                int n = text.CountOf(0, i, "\"");
                if ((n % 2) == 0)
                    return text.Substring(0, i);
            }

            return text;
        }
    }
}
