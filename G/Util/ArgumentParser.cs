using System.Collections.Generic;

namespace G.Util
{
	public class ArgumentParser
	{
		private List<string> arguments = new List<string>();
		public string[] Arguments { get { return arguments.ToArray(); } }
		public int ArgumentCount { get { return arguments.Count; } }

		private Dictionary<string, string> options = new Dictionary<string, string>();
		public int OptionCount { get { return options.Count; } }

		public ArgumentParser()
		{
		}

		public ArgumentParser(string[] args)
		{
			Parse(args);
		}

		public void Parse(string[] args)
		{
			foreach (var a in args)
			{
				if (a.StartsWith("--"))
				{
					string key = null;
					string value = null;

					string arg = a.Substring(2);

					int eqIndex = arg.IndexOf('=');
					if (eqIndex < 0)
					{
						key = arg;
					}
					else
					{
						key = arg.Substring(0, eqIndex);
						value = arg.Substring(eqIndex + 1);
					}

					options[key.ToLower()] = value;
				}
				else
				{
					arguments.Add(a);
				}
			}
		}

		public string GetArgument(int index)
		{
			if (index < 0 || index >= arguments.Count)
				return null;
			return arguments[index];
		}

		public string GetOption(string key)
		{
			string option;
			if (options.TryGetValue(key.ToLower(), out option))
				return option;
			else
				return null;
		}

		public bool IsOption(string key)
		{
			return options.ContainsKey(key.ToLower());
		}
	}
}
