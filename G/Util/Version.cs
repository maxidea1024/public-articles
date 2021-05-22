using System;

namespace G.Util
{
	public class Version
	{
		private ushort[] array;

		public Version()
		{
			array = new ushort[] { 0, 0, 0, 0 };
		}

		public Version(ushort[] version)
		{
			if (version == null)
			{
				array = new ushort[] { 0, 0, 0, 0 };
			}
			else
			{
				array = new ushort[4];

				int count = (version.Length > 4) ? 4 : version.Length;
				for (int i = 0; i < count; i++)
				{
					array[i] = version[i];
				}
			}
		}

		public Version(ushort v1, ushort v2, ushort v3, ushort v4)
		{
			array = new ushort[] { v1, v2, v3, v4 };
		}

		public Version(string version)
		{
			array = ToArray(version);
		}

		public int Check2(Version v)
		{
			if (v == null) return 0;
			if (v.array[0] != array[0]) return 0;
			if (v.array[1] != array[1]) return 1;
			if (v.array[2] != array[2]) return 2;
			if (v.array[3] != array[3]) return 3;
			return 4;
		}

		public int Check2(ushort[] version)
		{
			return Check2(new Version(version));
		}

		public int Check2(ushort v1, ushort v2, ushort v3, ushort v4)
		{
			return Check2(new Version(v1, v2, v3, v4));
		}

		public int Check2(string version)
		{
			return Check2(new Version(version));
		}

		#region Will be deprecated
		public bool Check(ushort[] version)
		{
			if (version == null) return false;
			if (version.Length < 2) return false;
			return (array[0] == version[0]) && (array[1] == version[1]);
		}

		public bool Check(ushort v1, ushort v2)
		{
			return (array[0] == v1) && (array[1] == v2);
		}

		public bool Check(string version)
		{
			return Check(ToArray(version));
		}

		public bool CheckExactly(ushort[] version)
		{
			if (version == null) return false;
			if (version.Length < 4) return false;
			return (array[0] == version[0]) && (array[1] == version[1]) && (array[2] == version[2]) && (array[3] == version[3]);
		}

		public bool CheckExactly(string version)
		{
			return CheckExactly(ToArray(version));
		}
		#endregion

		public ushort[] ToArray()
		{
			return array;
		}

		public override string ToString()
		{
			return ToString(array);
		}

		public static string ToString(ushort[] version)
		{
			if (version == null)
				return "0.0.0.0";
			else
				return string.Format("{0}.{1}.{2}.{3}", version[0], version[1], version[2], version[3]);
		}

		public static string ToString(Version version)
		{
			if (version == null)
				return "0.0.0.0";
			else
				return version.ToString();
		}

		public static ushort[] ToArray(string version)
		{
			var array = new ushort[4];

			if (!string.IsNullOrEmpty(version))
			{
				var tokens = version.Split('.');

				for (int i = 0; i < tokens.Length; i++)
				{
					array[i] = ushort.Parse(tokens[i]);
				}
			}

			return array;
		}

		public static ushort[] ToArray(Version version)
		{
			if (version == null)
				return new ushort[] { 0, 0, 0, 0 };
			else
				return version.ToArray();
		}
	}
}
