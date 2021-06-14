using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace G.MySql
{
	public class DbTransaction : IDisposable
	{
		private bool disposed;

		private bool isRollback = false;

		public MySqlConnection Connection { get; private set; }
		public MySqlTransaction Transaction { get; private set; }

		private DbTransaction(string connectionString)
		{
			Connection = new MySqlConnection(connectionString);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed) return;

			if (disposing)
			{
				if (Transaction != null)
				{
					if (isRollback)
						Transaction.Rollback();
					else
						Transaction.Commit();
				}

				if (Connection != null)
				{
					Connection.Close();
				}
			}

			disposed = true;
		}

		~DbTransaction()
		{
			Dispose(false);
		}

		public void Rollback()
		{
			isRollback = true;
		}

		public static DbTransaction Create(string connectionString)
		{
			DbTransaction tr = new DbTransaction(connectionString);
			tr.Connection.Open();
			tr.Transaction = tr.Connection.BeginTransaction();
			return tr;
		}

		public static async Task<DbTransaction> CreateAsync(string connectionString)
		{
			DbTransaction tr = new DbTransaction(connectionString);
			await tr.Connection.OpenAsync();
			tr.Transaction = await tr.Connection.BeginTransactionAsync();
			
			return tr;
		}
	}
}
