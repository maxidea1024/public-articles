using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Dapper;
using G.Util;
using G.MySql;

namespace G.Log
{
	public class Log
	{

		private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

		public static string ConnectionString { get; private set; }
		public static string ConnectionString4Slave { get; private set; }

		protected static HashSet<string> createdTables = new HashSet<string>();
		public static DateTime NextMonth { get; private set; }

		public static async Task InitializeAsync(string connectionString, string connectionString4Slave = null)
		{
			ConnectionString = connectionString;

			if (string.IsNullOrEmpty(connectionString4Slave))
				ConnectionString4Slave = connectionString;
			else
				ConnectionString4Slave = connectionString4Slave;

			var now = DateTime.UtcNow;
			await CreateDatabaseTablesAsync(now);

			NextMonth = DateTimeEx.GetNextMonth(now);
			await CreateDatabaseTablesAsync(NextMonth);

			_ = Task.Run(async () => {
				while (true)
				{
					await Task.Delay(TimeSpan.FromDays(1));

					var nextMonth = DateTimeEx.GetNextMonth(DateTime.UtcNow);
					if (nextMonth != NextMonth)
					{
						NextMonth = nextMonth;
						await CreateDatabaseTablesAsync(nextMonth);
					}
				}
			});
		}

		private static async Task CreateDatabaseTablesAsync(DateTime now)
		{
			var yearMonth = now.ToString("yyyyMM");
			var baseType = typeof(Log);

			var types = Assembly.GetEntryAssembly().GetTypes();
			foreach (var type in types)
			{
				var typeInfo = type.GetTypeInfo();
				var typeName = typeInfo.Name;
				if (typeName.StartsWith("LogBase`")) continue;

				var isSubclass = typeInfo.IsSubclassOf(baseType);
				if (isSubclass == false) continue;

				await CreateDatabaseTableAsync(yearMonth, type);
			}
		}

		private static async Task CreateDatabaseTableAsync(string yearMonth, Type type)
		{
			type.TypeInitializer?.Invoke(null, null);       // Static Constructor 호출

			var methodInfo = type.GetMethod("_CreateDatabaseTableAsync", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
			if (methodInfo != null)
			{
				await (Task)methodInfo.Invoke(null, new object[] { yearMonth });
			}
		}
	}
}
