using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace G.MySql
{
	public class DbReader : IDisposable
	{
		private bool disposed = false;

		private MySqlConnection connection;
		private MySqlCommand command;

		public MySqlDataReader Reader { get; private set; }

		private DbReader(string connectionString, string sql)
		{
			connection = new MySqlConnection(connectionString);
			command = new MySqlCommand(sql, connection);

			connection.Open();
			Reader = command.ExecuteReader();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposed) return;

			if (disposing)
			{
				Reader.Dispose();
				command.Dispose();
				connection.Dispose();
			}

			disposed = true;
		}

		public static DbReader Create(string connectionString, string sql)
		{
			DbReader reader = new DbReader(connectionString, sql);
			reader.connection.Open();
			reader.Reader = reader.command.ExecuteReader();
			return reader;
		}

		public static async Task<DbReader> CreateAsync(string connectionString, string sql)
		{
			DbReader reader = new DbReader(connectionString, sql);
			await reader.connection.OpenAsync();
			reader.Reader = (MySqlDataReader)await reader.command.ExecuteReaderAsync();
			return reader;
		}
	}
}
