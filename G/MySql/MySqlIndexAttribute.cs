using System;

namespace G.MySql
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class MySqlIndexAttribute : Attribute
	{
		public string[] FieldNames { get; set; }
		public string Name { get; set; }
		public bool IsUnique { get; set; }

		public MySqlIndexAttribute(string fieldNames, string name = null, bool isUnique = false)
		{
			if (!string.IsNullOrEmpty(fieldNames))
			{
				FieldNames = fieldNames.Split(',');
				for (int i = 0; i < FieldNames.Length; i++)
				{
					FieldNames[i] = FieldNames[i].Trim();
				}
			}

			Name = name;
			IsUnique = isUnique;
		}
	}
}
