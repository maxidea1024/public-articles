using System;
using System.Reflection;
using System.Collections.Generic;

namespace G.MySql
{
	public class MySqlTableInfo
	{
		public string TableName { get; set; }
		public string[] PrimaryKey { get; set; }
		public bool IsDevidedByMonth { get; set; }

		public List<MySqlIndexInfo> Indexes { get; set; }
		public List<MySqlBaseFieldInfo> Fields { get; set; }

        public List<PropertyInfo> Properties { get; set; }
    }
}
