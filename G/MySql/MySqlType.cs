using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace G.MySql
{
	public class MySqlType
	{
		private static Dictionary<Type, MySqlDbType> mapType2MySqlDbType;
		private static Dictionary<MySqlDbType, Type> mapMySqlDbType2Type;

		private static Dictionary<Type, string> mapType2MySqlType;

		static MySqlType()
		{
			mapType2MySqlDbType = new Dictionary<Type, MySqlDbType>();
			mapType2MySqlDbType[typeof(byte)] = MySqlDbType.UByte;
			mapType2MySqlDbType[typeof(sbyte)] = MySqlDbType.Byte;
			mapType2MySqlDbType[typeof(short)] = MySqlDbType.Int16;
			mapType2MySqlDbType[typeof(ushort)] = MySqlDbType.UInt16;
			mapType2MySqlDbType[typeof(int)] = MySqlDbType.Int32;
			mapType2MySqlDbType[typeof(uint)] = MySqlDbType.UInt32;
			mapType2MySqlDbType[typeof(long)] = MySqlDbType.Int64;
			mapType2MySqlDbType[typeof(ulong)] = MySqlDbType.UInt64;
			mapType2MySqlDbType[typeof(float)] = MySqlDbType.Float;
			mapType2MySqlDbType[typeof(double)] = MySqlDbType.Double;
			mapType2MySqlDbType[typeof(decimal)] = MySqlDbType.Decimal;
			mapType2MySqlDbType[typeof(bool)] = MySqlDbType.Bit;
			mapType2MySqlDbType[typeof(string)] = MySqlDbType.String;
			mapType2MySqlDbType[typeof(char)] = MySqlDbType.VarChar;
			mapType2MySqlDbType[typeof(Guid)] = MySqlDbType.Guid;
			mapType2MySqlDbType[typeof(DateTime)] = MySqlDbType.DateTime;
			mapType2MySqlDbType[typeof(DateTimeOffset)] = MySqlDbType.DateTime;
			mapType2MySqlDbType[typeof(byte[])] = MySqlDbType.Binary;
			mapType2MySqlDbType[typeof(byte?)] = MySqlDbType.UByte;
			mapType2MySqlDbType[typeof(sbyte?)] = MySqlDbType.Byte;
			mapType2MySqlDbType[typeof(short?)] = MySqlDbType.Int16;
			mapType2MySqlDbType[typeof(ushort?)] = MySqlDbType.UInt16;
			mapType2MySqlDbType[typeof(int?)] = MySqlDbType.Int32;
			mapType2MySqlDbType[typeof(uint?)] = MySqlDbType.UInt32;
			mapType2MySqlDbType[typeof(long?)] = MySqlDbType.Int64;
			mapType2MySqlDbType[typeof(ulong?)] = MySqlDbType.UInt64;
			mapType2MySqlDbType[typeof(float?)] = MySqlDbType.Float;
			mapType2MySqlDbType[typeof(double?)] = MySqlDbType.Double;
			mapType2MySqlDbType[typeof(decimal?)] = MySqlDbType.Decimal;
			mapType2MySqlDbType[typeof(bool?)] = MySqlDbType.Bit;
			mapType2MySqlDbType[typeof(char?)] = MySqlDbType.VarChar;
			mapType2MySqlDbType[typeof(Guid?)] = MySqlDbType.Guid;
			mapType2MySqlDbType[typeof(DateTime?)] = MySqlDbType.DateTime;
			mapType2MySqlDbType[typeof(DateTimeOffset?)] = MySqlDbType.DateTime;
            mapType2MySqlDbType[typeof(JArray)] = MySqlDbType.JSON;
            mapType2MySqlDbType[typeof(JObject)] = MySqlDbType.JSON;

            mapMySqlDbType2Type = new Dictionary<MySqlDbType, Type>();
			foreach (var i in mapType2MySqlDbType)
			{
				mapMySqlDbType2Type[i.Value] = i.Key;
			}

			mapType2MySqlType = new Dictionary<Type, string>();
			mapType2MySqlType[typeof(byte)] = "tinyint unsigned";
			mapType2MySqlType[typeof(sbyte)] = "tinyint";
			mapType2MySqlType[typeof(short)] = "smallint";
			mapType2MySqlType[typeof(ushort)] = "smallint unsigned";
			mapType2MySqlType[typeof(int)] = "int";
			mapType2MySqlType[typeof(uint)] = "int unsigned";
			mapType2MySqlType[typeof(long)] = "bigint";
			mapType2MySqlType[typeof(ulong)] = "bigint unsigned";
			mapType2MySqlType[typeof(float)] = "float";
			mapType2MySqlType[typeof(double)] = "double";
			mapType2MySqlType[typeof(decimal)] = "decimal";
			mapType2MySqlType[typeof(bool)] = "bit";
			mapType2MySqlType[typeof(string)] = "varchar";
			mapType2MySqlType[typeof(char)] = "varchar(1)";
			mapType2MySqlType[typeof(Guid)] = "varchar(37)";
			mapType2MySqlType[typeof(DateTime)] = "datetime";
			mapType2MySqlType[typeof(DateTimeOffset)] = "datetime";
			mapType2MySqlType[typeof(byte[])] = "binary";
			mapType2MySqlType[typeof(byte?)] = "tinyint unsigned";
			mapType2MySqlType[typeof(sbyte?)] = "tinyint";
			mapType2MySqlType[typeof(short?)] = "smallint";
			mapType2MySqlType[typeof(ushort?)] = "smallint unsigned";
			mapType2MySqlType[typeof(int?)] = "int";
			mapType2MySqlType[typeof(uint?)] = "int unsigned";
			mapType2MySqlType[typeof(long?)] = "bigint";
			mapType2MySqlType[typeof(ulong?)] = "bigint unsigned";
			mapType2MySqlType[typeof(float?)] = "float";
			mapType2MySqlType[typeof(double?)] = "double";
			mapType2MySqlType[typeof(decimal?)] = "decimal";
			mapType2MySqlType[typeof(bool?)] = "bit";
			mapType2MySqlType[typeof(char?)] = "varchar(1)";
			mapType2MySqlType[typeof(Guid?)] = "varchar(37)";
			mapType2MySqlType[typeof(DateTime?)] = "datetime";
			mapType2MySqlType[typeof(DateTimeOffset?)] = "datetime";
            mapType2MySqlType[typeof(JArray)] = "json";
            mapType2MySqlType[typeof(JObject)] = "json";
        }

		public static MySqlDbType ToMySqlDbType(Type type)
		{
			return mapType2MySqlDbType[type];
		}

		public static Type ToCSharpType(MySqlDbType type)
		{
			return mapMySqlDbType2Type[type];
		}

		public static string ToMySqlType(Type type, int length = 0)
		{
            if(type.IsEnum)
            {
                return "int";
            }

            if(!mapType2MySqlType.TryGetValue(type, out var mySqlType))
            {
                return null;
            }

			if (length > 0) return string.Format("{0}({1})", mySqlType, length);

			return mySqlType;
		}

		public static string ToCSharpType(string type, bool isNullable, bool isUnsigned)
		{
			if (isNullable)
			{
				switch (type)
				{
				case "bit": return "bool?";
				case "tinyint": return isUnsigned ? "byte?" : "sbyte?";
				case "smallint": return isUnsigned ? "ushort?" : "short?";
				case "int": return isUnsigned ? "uint?" : "int?";
				case "integer": return isUnsigned ? "uint?" : "int?";
				case "mediumint": return isUnsigned ? "ulong?" : "long?";
				case "bigint": return isUnsigned ? "ulong?" : "long?";
				case "real": return "double?";
				case "double": return "double?";
				case "float": return "float?";
				case "decimal": return "decimal?";
				case "numeric": return isUnsigned ? "ulong?" : "long?";
				case "date": return "DateTime?";
				case "time": return "DateTime?";
				case "datetime": return "DateTime?";
				case "timestamp": return "DateTime?";
				case "tinytext": return "string";
				case "text": return "string";
				case "mediumtext": return "string";
				case "longtext": return "string";
				case "varchar": return "string";
				case "char": return "string";
				case "void": return "void";
				case "binary": return "byte[]";
				case "varbinary": return "byte[]";
				case "tinyblob": return "byte[]";
				case "blob": return "byte[]";
				case "mediumblob": return "byte[]";
				case "longblob": return "byte[]";
				}
			}
			else
			{
				switch (type)
				{
				case "bit": return "bool";
				case "tinyint": return isUnsigned ? "byte" : "sbyte";
				case "smallint": return isUnsigned ? "ushort" : "short";
				case "int": return isUnsigned ? "uint" : "int";
				case "integer": return isUnsigned ? "uint" : "int";
				case "mediumint": return isUnsigned ? "ulong" : "long";
				case "bigint": return isUnsigned ? "ulong" : "long";
				case "real": return "double";
				case "double": return "double";
				case "float": return "float";
				case "decimal": return "decimal";
				case "numeric": return isUnsigned ? "ulong" : "long";
				case "date": return "DateTime";
				case "time": return "DateTime";
				case "datetime": return "DateTime";
				case "timestamp": return "DateTime";
				case "tinytext": return "string";
				case "text": return "string";
				case "mediumtext": return "string";
				case "longtext": return "string";
				case "varchar": return "string";
				case "char": return "string";
				case "void": return "void";
				case "binary": return "byte[]";
				case "varbinary": return "byte[]";
				case "tinyblob": return "byte[]";
				case "blob": return "byte[]";
				case "mediumblob": return "byte[]";
				case "longblob": return "byte[]";
				}
			}

			throw new Exception(String.Format("Error : Unknown Type {0}", type));
		}

		public static string ToMySqlDbType(string type, bool isUnsigned)
		{
			switch (type)
			{
			case "bit": return "MySqlDbType.Bit";
			case "tinyint": return isUnsigned ? "MySqlDbType.UByte" : "MySqlDbType.Byte";
			case "smallint": return isUnsigned ? "MySqlDbType.UInt16" : "MySqlDbType.Int16";
			case "int": return isUnsigned ? "MySqlDbType.UInt32" : "MySqlDbType.Int32";
			case "integer": return isUnsigned ? "MySqlDbType.UInt32" : "MySqlDbType.Int32";
			case "mediumint": return isUnsigned ? "MySqlDbType.UInt64" : "MySqlDbType.Int64";
			case "bigint": return isUnsigned ? "MySqlDbType.UInt64" : "MySqlDbType.Int64";
			case "real": return "MySqlDbType.Double";
			case "double": return "MySqlDbType.Double";
			case "float": return "MySqlDbType.Float";
			case "decimal": return "MySqlDbType.Decimal";
			case "date": return "MySqlDbType.Date";
			case "time": return "MySqlDbType.Time";
			case "datetime": return "MySqlDbType.DateTime";
			case "timestamp": return "MySqlDbType.Timestamp";
			case "varchar": return "MySqlDbType.VarChar";
			case "tinytext": return "MySqlDbType.TinyText";
			case "text": return "MySqlDbType.Text";
			case "mediumtext": return "MySqlDbType.MediumText";
			case "longtext": return "MySqlDbType.LongText";
			case "char": return "MySqlDbType.Char";
			case "binary": return "MySqlDbType.Binary";
			case "varbinary": return "MySqlDbType.VarBinary";
			case "tinyblob": return "MySqlDbType.TinyBlob";
			case "blob": return "MySqlDbType.Blob";
			case "mediumblob": return "MySqlDbType.MediumBlob";
			case "longblob": return "MySqlDbType.LongBlob";
			}

			throw new Exception(String.Format("Error : Unknown Type {0}", type));
		}

		public static bool IsString(string type)
		{
			switch (type)
			{
			case "varchar":
			case "tinytext":
			case "text":
			case "mediumtext":
			case "longtext":
				return true;
			default:
				return false;
			}
		}

		public static bool IsBinary(string type)
		{
			switch (type)
			{
			case "binary":
			case "varbinary":
			case "tinyblob":
			case "blob":
			case "mediumblob":
			case "longblob":
				return true;
			default:
				return false;
			}
		}

		public static bool IsBoolean(string type)
		{
			return (type == "bit");
		}

		public static bool HasLength(string type)
		{
			return IsString(type) || IsBinary(type);
		}

		public static string GetDefaultValue(string type)
		{
			switch (type)
			{
			case "bit": return "false";
			case "tinyint": return "0";
			case "smallint": return "0";
			case "int": return "0";
			case "integer": return "0";
			case "mediumint": return "0";
			case "bigint": return "0";
			case "double": return "0";
			case "real": return "0";
			case "float": return "0";
			case "decimal": return "0";
			case "date": return "DateTime.MinValue";
			case "time": return "DateTime.MinValue";
			case "datetime": return "DateTime.MinValue";
			case "timestamp": return "DateTime.MinValue";
			case "varchar": return "null";
			case "tinytext": return "null";
			case "text": return "null";
			case "mediumtext": return "null";
			case "longtext": return "null";
			case "char": return "null";
			case "binary": return "null";
			case "varbinary": return "null";
			case "tinyblob": return "null";
			case "blob": return "null";
			case "mediumblob": return "null";
			case "longblob": return "null";
			}

			throw new Exception(String.Format("Error : Unknown Type {0}", type));
		}

        public static string ToInCondition<T>(IEnumerable<T> conditions)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append(" (");
            foreach (var condition in conditions)
            {
                if(typeof(T) == typeof(string))
                {
                    builder.Append($"'{condition}'");
                }
                else if(typeof(T) == typeof(DateTime))
                {
                    var item = condition as DateTime?;
                    builder.Append($"'{item?.ToString("yyyy-MM-dd HH:mm:ss")}'");
                }
                else
                {
                    builder.Append(condition);
                }

                builder.Append(',');
            }
            builder.Replace(',', ')', builder.Length - 1, 1);
            return builder.ToString();
        }
	}
}
