using System;

namespace G.MySql
{
	[AttributeUsage(AttributeTargets.Property)]
	public class MySqlFieldAttribute : MySqlBaseFieldAttribute
    {
		public bool IsAutoIncrement { get; set; }

		public MySqlFieldAttribute(int index, string fieldName = null, string fieldType = null, bool isNullable = false, bool isAutoIncrement = false)
		{
			Index = index;
			FieldName = fieldName;
			FieldType = fieldType;
			IsNullable = isNullable;
			IsAutoIncrement = isAutoIncrement;
		}
	}

    [AttributeUsage(AttributeTargets.Property)]
    public class MySqlVirtualFieldAttribute : MySqlBaseFieldAttribute
    {
        public string Expression { get; set; }
        public MySqlColumnStorage Storage { get; set; }

        public MySqlVirtualFieldAttribute(int index, string expression, string fieldName = null, string fieldType = null, MySqlColumnStorage storage = MySqlColumnStorage.Virtual)
        {
            Index = index;
            FieldName = fieldName;
            FieldType = fieldType;
            Expression = expression;
            Storage = storage;
        }
    }

    public class MySqlBaseFieldAttribute : Attribute
    {
        public int Index { get; set; }
        public string FieldName { get; set; }
        public string FieldType { get; set; }
        public bool IsNullable { get; set; }
    }
}
