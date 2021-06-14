using System;
using System.Text;
using System.Collections.Generic;

namespace G.Web
{
	public class HttpParameters
	{
		private List<KeyValuePair<string, string>> keyValues = new List<KeyValuePair<string, string>>();

		public bool IsEmpty { get { return (keyValues.Count == 0); } }
		public int Count { get { return keyValues.Count; } }

		public void Clear()
		{
			keyValues.Clear();
		}

		public bool RemoveAt(int index)
		{
			if (index < keyValues.Count)
			{
				keyValues.RemoveAt(index);
				return true;
			}
			return false;
		}

		public KeyValuePair<string, string> GetAt(int index)
		{
			if (index < keyValues.Count)
				return keyValues[index];
			else
				throw new IndexOutOfRangeException();
		}

		public void Add(string key, object value)
		{
			keyValues.Add(new KeyValuePair<string, string>(key, value.ToString()));
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			foreach (var kv in keyValues)
			{
				yield return kv;
			}
		}

		public override string ToString()
		{
			return ToString(false);
		}

		public string ToString(bool withQuestion)
		{
			StringBuilder sb = new StringBuilder();

			bool isFirst = true;
			foreach (var kv in keyValues)
			{
				if (isFirst)
				{
					isFirst = false;
					if (withQuestion) sb.Append("?");
				}
				else
					sb.Append("&");
				
				sb.Append(kv.Key + "=" + kv.Value);
			}

			return sb.ToString();
		}

		public byte[] ToBytes()
		{
			return Encoding.UTF8.GetBytes(ToString(false));
		}
	}
}
