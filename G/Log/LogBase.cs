using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Dapper;
using G.Util;
using G.MySql;

namespace G.Log
{
	public class LogBase<T> : Log
	{
		private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		protected static SemaphoreLock semaphoreLock = new SemaphoreLock(TimeSpan.FromSeconds(20));

		protected static MySqlTableInfo tableInfo;
		protected static string sqlToInsert;
		protected static string sqlToUpsert;
        protected static string sqlToInsertMany;

		public static string YearMonth { get; protected set; }
		public static string TriggerAfterInsert { get; protected set; }

		[MySqlField(0, "seq", "bigint", IsAutoIncrement = true)]
		public long Seq { get; set; }

		[MySqlField(int.MaxValue, "time", "datetime")]
		public DateTime Time { get; set; }

		public LogBase()
		{
			Time = DateTime.UtcNow;
		}

		static LogBase()
		{
			tableInfo = MySqlDapper.ParseTable(typeof(T));
		}

		public static async Task<(string SqlToInsert, string SqlToUpsert, string SqlToInsertMany)> GetSqlsAsync(DateTime time)
		{
			using (await semaphoreLock.LockAsync())
			{
				var ym = tableInfo.IsDevidedByMonth ? time.ToString("yyyyMM") : string.Empty;
				if (ym != YearMonth)
				{
					YearMonth = ym;

					sqlToInsertMany = MySqlDapper.GetSqlToInsertBase(ym, tableInfo);
					sqlToInsert = MySqlDapper.GetSqlToInsert(tableInfo, sqlToInsertMany);
					sqlToUpsert = MySqlDapper.GetSqlToUpsert(ym, tableInfo);

					await _CreateDatabaseTableAsync(ym);
				}

				return (sqlToInsert, sqlToUpsert, sqlToInsertMany);
			}
		}

		protected static async Task<bool> _CreateDatabaseTableAsync(string yearMonth)
		{
			var ym = tableInfo.IsDevidedByMonth ? yearMonth : string.Empty;
			var nameWithYearMonth = tableInfo.TableName + ym;

			if (createdTables.Contains(nameWithYearMonth)) return true;

			bool result = true;

			try
			{
				string sqlToCreate = MySqlDapper.GetSqlToCreate(ym, tableInfo);
				await using (var conn = new MySqlConnection(ConnectionString))
				{
					await conn.ExecuteAsync(sqlToCreate);
				}

				if (!string.IsNullOrEmpty(TriggerAfterInsert))
				{
					await using (var conn = new MySqlConnection(ConnectionString))
					{
						var sqlAfterInsert = MySqlDapper.GetSqlToCreateTriggerAfrterInsert(ym, tableInfo, TriggerAfterInsert);
						await conn.ExecuteAsync(sqlAfterInsert);
					}
				}
			}
			catch (MySqlException ex)
			{
				if (ex.Number != 1359)      // Trigger already exists
				{
					log.Error(ex);
					result = false;
				}
			}
			catch (Exception ex)
			{
				log.Error(ex);
				result = false;
			}

			if (result) createdTables.Add(nameWithYearMonth);

			log.Debug($"Log Check : {nameWithYearMonth} - " + (result ? "OK" : "Failed"));
			return result;
		}

		// Not support json type now
		private static async Task<(string query, DynamicParameters parameters)> CreateInsertManyQueryAsync(IEnumerable<T> items)
        {
			var sqls = await GetSqlsAsync(DateTime.UtcNow);

			DynamicParameters queryParameters = new DynamicParameters();

            var properties = tableInfo.Properties;
            var rows = new List<string>();

            int i = 0;
            foreach (var item in items)
            {
                var row = new List<string>();

                foreach (var property in properties)
                {
                    var column = $"@{property.Name}_{i}";
                    row.Add(column);

					TypeCode typeCode = Type.GetTypeCode(property.PropertyType);
					if (typeCode == TypeCode.Object)
						queryParameters.Add(column, Newtonsoft.Json.JsonConvert.SerializeObject(property.GetValue(item)));
					else
						queryParameters.Add(column, property.GetValue(item));
				}

                rows.Add($"({string.Join(',', row)})");
                i++;
            }

            var query = $"{sqls.SqlToInsertMany} {string.Join(',', rows)};";

            return (query, queryParameters);
        }

        public static async Task<int> InsertManyAsync(IEnumerable<T> items)
        {
			try
			{
				var result = await CreateInsertManyQueryAsync(items);
				return await MySqlDapper.ExecuteAsync(ConnectionString, result.query, result.parameters);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return 0;
			}
        }

        public async Task<bool> InsertAsync()
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);

				int result = await MySqlDapper.ExecuteAsync(ConnectionString, sqls.SqlToInsert, this);
				return (result > 0);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return false;
			}
		}

		public async Task<bool> InsertAsync(MySqlConnection conn)
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);

				int result = await MySqlDapper.ExecuteAsync(conn, sqls.SqlToInsert, this);
				return (result > 0);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return false;
			}
		}

		public async Task<bool> InsertAsync(DbTransaction dbt)
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);

				int result = await MySqlDapper.ExecuteAsync(dbt, sqls.SqlToInsert, this);
				return (result > 0);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return false;
			}
		}

		public async Task<bool> UpsertAsync()
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);

				int result = await MySqlDapper.ExecuteAsync(ConnectionString, sqls.SqlToUpsert, this);
				return (result > 0);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return false;
			}
		}

		public async Task<bool> UpsertAsync(MySqlConnection conn)
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);

				int result = await MySqlDapper.ExecuteAsync(conn, sqls.SqlToUpsert, this);
				return (result > 0);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return false;
			}
		}

		public async Task<bool> UpsertAsync(DbTransaction dbt)
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);

				int result = await MySqlDapper.ExecuteAsync(dbt, sqls.SqlToUpsert, this);
				return (result > 0);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
				return false;
			}
		}

		public static async Task<List<T>> QueryAsync(string yearMonth, string where = null, string orderBy = null)
		{
			string sql = MySqlDapper.GetSqlToQuery(yearMonth, tableInfo, where, orderBy);
			return await MySqlDapper.QueryAsync<T>(ConnectionString, sql);
		}

		public class PageData<S>
		{
			public int TotalRow { get; set; }
			public int TotalPage { get; set; }
			public List<S> List { get; set; }
		}

		public static async Task<PageData<T>> QueryPageAsync(string yearMonth, int page, int size, string where = null, string orderBy = null)
		{
			await using (var conn = new MySqlConnection(ConnectionString4Slave))
			{
				string sql1 = MySqlDapper.GetSqlToQueryTotalRow(yearMonth, tableInfo, where);
				int totalRow = await conn.ExecuteScalarAsync<int>(sql1);

				int totalPage = (int)Math.Floor((decimal)(totalRow + size - 1) / size);
				int offset = page * size;

				string sql2 = MySqlDapper.GetSqlToQueryPage(yearMonth, tableInfo, offset, size, where, orderBy);
				var list = await MySqlDapper.QueryAsync<T>(ConnectionString4Slave, sql2);

				return new PageData<T>()
				{
					TotalRow = totalRow,
					TotalPage = totalPage,
					List = list
				};
			}
		}
		

		#region Oneway
		public async Task OnewayInsertAsync()
		{
			try
			{
				var sqls = await GetSqlsAsync(Time);
				await MySqlDapper.ExecuteAsync(ConnectionString, sqls.SqlToInsert, this);
			}
			catch (Exception e)
			{
				log.Error(e.Message);
			}
		}

		public static async Task OnewayInsertManyAsync(IEnumerable<T> items)
		{
			try
			{
				var (query, parameters) = await CreateInsertManyQueryAsync(items);
				await MySqlDapper.ExecuteAsync(ConnectionString, query, parameters);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
			}
		}
		#endregion
	}
}
