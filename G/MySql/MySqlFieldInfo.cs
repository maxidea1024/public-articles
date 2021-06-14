using System;
using G.MySql;

namespace G.MySql
{
    public enum MySqlColumnStorage
    {
        Virtual = 0,
        Stored = 1
    }

    public class MySqlBaseFieldInfo
    {
        public bool IsVirtualField { get; protected set; }
        public int Index { get; set; }
        public string FieldName { get; set; }
        public virtual string FieldType { get; set; }
        public bool IsNullable { get; set; }
    }

    public class MySqlFieldInfo : MySqlBaseFieldInfo
    {
        public MySqlFieldInfo()
        {
            IsVirtualField = false;
        }

		private string fieldType;
		public override string FieldType
		{
			get
			{
				if (string.IsNullOrEmpty(fieldType))
				{
					fieldType = MySqlType.ToMySqlDbType(Type).ToString();
					return fieldType;
				}
				else
				{
					return fieldType;
				}
			}

			set
			{
				fieldType = value;
			}
		}

		public bool IsAutoIncrement { get; set; }
		public string Name { get; set; }
		public Type Type { get; set; }
	}

    public class MySqlVirtualFieldInfo : MySqlBaseFieldInfo
    {
        public MySqlVirtualFieldInfo()
        {
            IsVirtualField = true;
        }

        public string Expression { get; set; }
        public MySqlColumnStorage Storage { get; set; }
    }
}
