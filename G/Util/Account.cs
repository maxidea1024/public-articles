using System;
using System.Text;

namespace G.Util
{
	public class Account
	{
		private readonly string account;

		public long Suid { get; private set; }

		public Account()
		{
			var bytes = new byte[8];
			new Randomizer().NextBytes(bytes);
			bytes[7] &= 0x0F;

			Suid = BitConverter.ToInt64(bytes, 0);

			bytes[7] <<= 4;
			account = Coupon.ToString(bytes);
		}

		public Account(string account)
		{
			this.account = account;

			var bytes = Coupon.FromString(account);
			if (bytes.Length != 8)
				throw new ArgumentException("Account is wrong.");

			if ((bytes[7] & 0x0F) != 0)
				throw new ArgumentException("Account is wrong.");

			bytes[7] >>= 4;
			Suid = BitConverter.ToInt64(bytes, 0);
		}

		// 0 ~ 0x0FFFFFFFFFFFFFFF
		public Account(long suid, int dash = 0)
		{
			var bytes = BitConverter.GetBytes(suid);
			if ((bytes[7] & 0xF0) != 0)
				throw new ArgumentException("Suid can't be minus.");

			Suid = suid;

			bytes[7] <<= 4;
			account = Coupon.ToString(bytes, dash);
		}

		public override string ToString()
		{
			return account;
		}

		public string ToString(int dash)
		{
			if (dash <= 0) return account;

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < account.Length; i++)
			{
				if (i != 0 && (i % dash) == 0) sb.Append('-');
				sb.Append(account[i]);
			}

			return sb.ToString();
		}

		public override bool Equals(object obj)
		{
			if (obj == null) return false;

			Account acc = obj as Account;
			if (acc == null) return false;

			return (Suid == acc.Suid);
		}

		public static bool operator ==(Account a, Account b)
		{
			if (object.ReferenceEquals(a, b)) return true;
			if ((object)a == null || (object)b == null) return false;
			return a.Equals(b);
		}

		public static bool operator !=(Account a, Account b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return account.GetHashCode();
		}

		public static long ToSuid(string account)
		{
			try
			{
				return new Account(account).Suid;
			}
			catch (Exception)
			{
				return -1;
			}
		}

		public static string ToString(long suid, int dash)
		{
			try
			{
				if (dash > 0)
					return new Account(suid).ToString(dash);
				else
					return new Account(suid).ToString();
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static Account New
		{
			get
			{
				return new Account();
			}
		}
	}
}
