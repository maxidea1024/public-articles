using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Dapper;
using G.Util;


namespace G.MySql
{
    public class MySqlDapper
    {
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        static MySqlDapper()
        {
            var jsonHandler = new MySqlJsonTypeHandler();
            SqlMapper.AddTypeHandler(typeof(JArray), jsonHandler);
            SqlMapper.AddTypeHandler(typeof(JObject), jsonHandler);
        }

		public static MySqlTableInfo ParseTable<T>() where T : class
		{
			return ParseTable(typeof(T));
		}

		public static MySqlTableInfo ParseTable(Type type)
		{
			try
			{
				MySqlTableInfo tableInfo = new MySqlTableInfo();
				tableInfo.Indexes = new List<MySqlIndexInfo>();

				TypeInfo typeInfo = type.GetTypeInfo();

				foreach (var attr in typeInfo.GetCustomAttributes<MySqlTableAttribute>())
				{
					tableInfo.TableName = attr.TableName;
					tableInfo.PrimaryKey = attr.PrimaryKey;
					tableInfo.IsDevidedByMonth = attr.IsDevidedByMonth;
					break;
				}

				foreach (var attr in typeInfo.GetCustomAttributes<MySqlIndexAttribute>())
				{
					var indexInfo = new MySqlIndexInfo();
					indexInfo.FieldNames = attr.FieldNames;

					if (string.IsNullOrEmpty(attr.Name))
					{
						StringBuilder sb = new StringBuilder();
						sb.Append("idx");
						foreach (var fieldName in indexInfo.FieldNames)
							sb.Append(StringEx.ToUpperFirstCharacter(fieldName));
						indexInfo.Name = sb.ToString();
					}
					else
					{
						indexInfo.Name = attr.Name;
					}

					indexInfo.IsUnique = attr.IsUnique;

					tableInfo.Indexes.Add(indexInfo);
				}

				if (string.IsNullOrEmpty(tableInfo.TableName))
					tableInfo.TableName = StringEx.ToLowerFirstCharacter(type.Name);

				var fields = new SortedDictionary<long, MySqlBaseFieldInfo>();
				var properties = new SortedDictionary<long, PropertyInfo>();

				foreach (var p in type.GetProperties())
				{
					var attrs = p.GetCustomAttributes(false);
					foreach (var attr in attrs)
					{
						MySqlFieldInfo fieldInfo = new MySqlFieldInfo();

						var attr1 = attr as MySqlFieldAttribute;
						if (attr1 != null)
						{
							fieldInfo.Index = attr1.Index;

							if (string.IsNullOrEmpty(attr1.FieldName))
								fieldInfo.FieldName = StringEx.ToLowerFirstCharacter(p.Name);
							else
								fieldInfo.FieldName = attr1.FieldName;

							if (string.IsNullOrEmpty(attr1.FieldType))
								fieldInfo.FieldType = MySqlType.ToMySqlType(p.PropertyType);
							else
								fieldInfo.FieldType = attr1.FieldType;

							fieldInfo.IsNullable = attr1.IsNullable;
							fieldInfo.IsAutoIncrement = attr1.IsAutoIncrement;
							fieldInfo.Name = p.Name;
							fieldInfo.Type = p.PropertyType;

							if (fieldInfo.IsNullable == false)
							{
								var t = p.PropertyType.GetTypeInfo();
								if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
									fieldInfo.IsNullable = true;
							}

							if (!string.IsNullOrEmpty(fieldInfo.FieldName))
							{
								fields.Add(fieldInfo.Index, fieldInfo);

								if (!fieldInfo.IsAutoIncrement)
									properties.Add(fieldInfo.Index, p);

								break;
							}
						}

						var attr2 = attr as MySqlVirtualFieldAttribute;
						if (attr2 != null)
						{
							MySqlVirtualFieldInfo virtualFieldInfo = new MySqlVirtualFieldInfo();

							virtualFieldInfo.Index = attr2.Index;

							if (string.IsNullOrEmpty(attr2.FieldName))
								virtualFieldInfo.FieldName = StringEx.ToLowerFirstCharacter(p.Name);
							else
								virtualFieldInfo.FieldName = attr2.FieldName;

							if (string.IsNullOrEmpty(attr2.FieldType))
								virtualFieldInfo.FieldType = MySqlType.ToMySqlType(p.PropertyType);
							else
								virtualFieldInfo.FieldType = attr2.FieldType;

							virtualFieldInfo.IsNullable = attr2.IsNullable;
							virtualFieldInfo.Expression = attr2.Expression;
							virtualFieldInfo.Storage = attr2.Storage;

							if (virtualFieldInfo.IsNullable == false)
							{
								var t = p.PropertyType.GetTypeInfo();
								if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
									virtualFieldInfo.IsNullable = true;
							}

							if (!string.IsNullOrEmpty(virtualFieldInfo.FieldName))
							{
								fields.Add(virtualFieldInfo.Index, virtualFieldInfo);
								break;
							}
						}
					}
				}

				tableInfo.Fields = fields.Values.ToList();
				tableInfo.Properties = properties.Values.ToList();

				return tableInfo;
			}
			catch (Exception ex)
			{
				log.Error(ex);
				throw;
			}
		}

		public static string GetSqlToCreate<T>(string yearMonth)
		{
			var tableInfo = ParseTable(typeof(T));
			return GetSqlToCreate(yearMonth, tableInfo);
		}

		public static string GetSqlToCreate(string yearMonth, MySqlTableInfo tableInfo)
		{
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();

			sb.Append($"create table if not exists `{tableInfo.TableName}{yearMonth}` (");

			bool isFirst = true;
			foreach (var f in tableInfo.Fields)
			{
				if (isFirst)
					isFirst = false;
				else
					sb.Append(", ");

				sb.Append("`" + f.FieldName + "`");
				sb.Append(" " + f.FieldType);

				if (f.IsVirtualField)
				{
					var vf = (MySqlVirtualFieldInfo)f;
					sb.Append($" generated always as ({vf.Expression}) {vf.Storage}");
				}

				if (f.IsNullable)
					sb.Append(" null");
				else
					sb.Append(" not null");

				if (!f.IsVirtualField)
				{
					var rf = (MySqlFieldInfo)f;
					if (rf.IsAutoIncrement)
					{
						sb.Append(" auto_increment");
					}
				}
			}

			if (tableInfo.PrimaryKey != null && tableInfo.PrimaryKey.Length > 0)
			{
				sb.Append(", primary key (");
				bool isFirst2 = true;
				foreach (var p in tableInfo.PrimaryKey)
				{
					if (isFirst2)
						isFirst2 = false;
					else
						sb.Append(", ");

					sb.Append("`" + p + "`");
				}
				sb.Append(")");
			}

			if (tableInfo.Indexes != null && tableInfo.Indexes.Count > 0)
			{
				foreach (var idx in tableInfo.Indexes)
				{
					sb.Append(", ");

					if (idx.IsUnique) sb.Append(" unique");

					sb.Append(" index ");
					sb.Append(idx.Name);
					sb.Append("(");

					bool isFirst2 = true;
					foreach (var fieldName in idx.FieldNames)
					{
						if (isFirst2)
							isFirst2 = false;
						else
							sb.Append(", ");

						sb.Append("`" + fieldName + "`");
					}

					sb.Append(")");
				}
			}

			sb.Append(")");

			return sb.ToString();
		}

        public static string GetSqlToInsert<T>(string yearMonth)
		{
			var tableInfo = ParseTable(typeof(T));
			return GetSqlToInsert(yearMonth, tableInfo);
		}

        public static string GetSqlToInsertBase(string yearMonth, MySqlTableInfo tableInfo)
        {
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();

			sb.Append($"insert ignore into `{tableInfo.TableName}{yearMonth}` (");
			//sb.Append($"insert ignore into `{tableInfo.TableName}` ("); // 임시로 
			bool isFirst = true;
			foreach (var f in tableInfo.Fields)
			{
				if (f.IsVirtualField) continue;

				var rf = (MySqlFieldInfo)f;
				if (rf.IsAutoIncrement) continue;

				if (isFirst)
					isFirst = false;
				else
					sb.Append(", ");

				sb.Append($"`{rf.FieldName}`");
			}
			sb.Append(") values ");

			return sb.ToString();
		}

        public static string GetSqlToInsert(MySqlTableInfo tableInfo, string insertBase)
        {
			var sb = new StringBuilder();

            sb.Append(insertBase);
            sb.Append('(');

            bool isFirst = true;
            foreach (var f in tableInfo.Fields)
            {
                if (f.IsVirtualField) continue;

                var rf = (MySqlFieldInfo)f;
                if (rf.IsAutoIncrement) continue;

                if (isFirst)
                    isFirst = false;
                else
                    sb.Append(", ");

                sb.Append("@" + rf.Name);
            }
            sb.Append(')');

            return sb.ToString();
        }

        public static string GetSqlToInsert(string yearMonth, MySqlTableInfo tableInfo)
		{
            return GetSqlToInsert(tableInfo, GetSqlToInsertBase(yearMonth, tableInfo));
		}

		public static string GetSqlToUpsert(string yearMonth, MySqlTableInfo tableInfo)
		{
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();

			sb.Append($"insert into `{tableInfo.TableName}{yearMonth}` (");
			//sb.Append($"insert into `{tableInfo.TableName}` ("); // 임시로..
			bool isFirst = true;
			foreach (var f in tableInfo.Fields)
			{
                if (f.IsVirtualField) continue;

                var rf = (MySqlFieldInfo)f;
                if (rf.IsAutoIncrement) continue;

				if (isFirst)
					isFirst = false;
				else
					sb.Append(", ");
				
				sb.Append($"`{f.FieldName}`");
			}
			sb.Append(") values (");
			isFirst = true;
			foreach (var f in tableInfo.Fields)
			{
                if (f.IsVirtualField) continue;

                var rf = (MySqlFieldInfo)f;
                if (rf.IsAutoIncrement) continue;

				if (isFirst)
					isFirst = false;
				else
					sb.Append(", ");

				sb.Append("@" + rf.Name);
			}
			sb.Append(")");

			sb.Append(" on duplicate key update ");
			isFirst = true;
			foreach (var f in tableInfo.Fields)
			{
                if (f.IsVirtualField) continue;
				if (tableInfo.PrimaryKey != null && tableInfo.PrimaryKey.Contains(f.FieldName)) continue;

				if (isFirst)
					isFirst = false;
				else
					sb.Append(", ");

                var rf = (MySqlFieldInfo)f;
				sb.Append($"`{rf.FieldName}` = @{rf.Name}");
			}

			return sb.ToString();
		}

		public static string GetSqlToCreateTriggerAfrterInsert(string yearMonth, MySqlTableInfo tableInfo, string triggerAfterInsert)
		{
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();

			sb.AppendLine($"create trigger `{tableInfo.TableName}{yearMonth}AfterInsert` after insert on `{tableInfo.TableName}{yearMonth}` for each row");
			sb.AppendLine("begin");
			sb.AppendLine(triggerAfterInsert);
			sb.AppendLine("end");

			return sb.ToString();
		}

		public static string GetSqlToQuery<T>(string yearMonth, string where = null, string orderBy = null)
		{
			var tableInfo = ParseTable(typeof(T));
			return GetSqlToQuery(yearMonth, tableInfo, where, orderBy);
		}

		public static string GetSqlToQuery(string yearMonth, MySqlTableInfo tableInfo, string where = null, string orderBy = null)
		{
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();
			sb.Append("select * from " + tableInfo.TableName + yearMonth);

			if (!string.IsNullOrEmpty(where))
				sb.Append(" where " + where);
			
			if (!string.IsNullOrEmpty(orderBy))
				sb.Append(" order by " + orderBy);

			return sb.ToString();
		}

		public static string GetSqlToQueryTotalRow<T>(string yearMonth, string where = null)
		{
			var tableInfo = ParseTable(typeof(T));
			return GetSqlToQueryTotalRow(yearMonth, tableInfo, where);
		}

		public static string GetSqlToQueryTotalRow(string yearMonth, MySqlTableInfo tableInfo, string where = null)
		{
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();
			sb.Append("select count(*) from ");
			sb.Append(tableInfo.TableName + yearMonth);

			if (!string.IsNullOrEmpty(where))
				sb.Append(" where " + where);

			return sb.ToString();
		}

		public static string GetSqlToQueryPage<T>(string yearMonth, int offset, int limit, string where = null, string orderBy = null)
		{
			var tableInfo = ParseTable(typeof(T));
			return GetSqlToQueryPage(yearMonth, tableInfo, offset, limit, where, orderBy);
		}

		public static string GetSqlToQueryPage(string yearMonth, MySqlTableInfo tableInfo, int offset, int limit, string where = null, string orderBy = null)
		{
			if (yearMonth == null) yearMonth = string.Empty;

			var sb = new StringBuilder();
			sb.Append("select * from " + tableInfo.TableName + yearMonth);

			if (!string.IsNullOrEmpty(where))
				sb.Append(" where " + where);
			
			if (!string.IsNullOrEmpty(orderBy))
				sb.Append(" order by " + orderBy);

			sb.Append($" limit {offset}, {limit}");

			return sb.ToString();
		}

		public static List<T> Query<T>(string connectionString, string sql, object param = null)
		{
			using (var conn = new MySqlConnection(connectionString))
			{
				var list = conn.Query<T>(sql, param);
				return list.AsList();
			}
		}

		public static List<T> Query<T>(MySqlConnection conn, string sql, object param = null)
		{
			var list = conn.Query<T>(sql, param);
			return list.AsList();
		}

		public static List<T> Query<T>(DbTransaction dbt, string sql, object param = null)
		{
			var list = dbt.Connection.Query<T>(sql, param, dbt.Transaction);
			return list.AsList();
		}

		public static async Task<List<T>> QueryAsync<T>(string connectionString, string sql, object param = null)
		{
			await using (var conn = new MySqlConnection(connectionString))
			{
				var list = await conn.QueryAsync<T>(sql, param);
				return list.AsList();
			}
		}

		public static async Task<List<T>> QueryAsync<T>(MySqlConnection conn, string sql, object param = null)
		{
			var list = await conn.QueryAsync<T>(sql, param);
			return list.AsList();
		}

		public static async Task<List<T>> QueryAsync<T>(DbTransaction dbt, string sql, object param = null)
		{
			var list = await dbt.Connection.QueryAsync<T>(sql, param, dbt.Transaction);
			return list.AsList();
		}

        public static int Execute(string connectionString, string sql, object param = null)
        {
            using (var conn = new MySqlConnection(connectionString))
            {
                return conn.Execute(sql, param);
            }
        }

        public static int Execute(MySqlConnection conn, string sql, object param = null)
        {
            return conn.Execute(sql, param);
        }

        public static int Execute(DbTransaction dbt, string sql, object param = null)
        {
            return dbt.Connection.Execute(sql, param, dbt.Transaction);
        }

        public static async Task<int> ExecuteAsync(string connectionString, string sql, object param = null)
        {
			try
			{
				await using (var conn = new MySqlConnection(connectionString))
				{
					return await conn.ExecuteAsync(sql, param);
				}
			}
			catch (Exception ex)
			{
				log.Error(ex);
				throw;
			}
        }

        public static async Task<int> ExecuteAsync(MySqlConnection conn, string sql, object param = null)
        {
			try
			{
				return await conn.ExecuteAsync(sql, param);
			}
			catch (Exception ex)
			{
				log.Error(ex);
				throw;
			}
        }

        public static async Task<int> ExecuteAsync(DbTransaction dbt, string sql, object param = null)
        {
			try
			{
				return await dbt.Connection.ExecuteAsync(sql, param, dbt.Transaction);
			}
			catch (Exception ex)
			{
				log.Error(ex);
				throw;
			}
        }
    }
}
