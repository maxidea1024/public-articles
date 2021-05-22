using System;
using System.Collections.Generic;
using System.Globalization;

namespace G.Util
{
	public static class DateTimeEx
	{
		public static long TicksForMillisecond { get; } = 10000;
		public static long TicksForSecond { get; } = 10000000;
		public static long TicksForMinute { get; } = 600000000;
		public static long TicksForHour { get; } = 36000000000;

		public static DateTime TrimBelowMillisecond(DateTime dt)
		{
			return new DateTime(dt.Ticks / TicksForMillisecond * TicksForMillisecond);
		}

		public static DateTime TrimBelowSecond(DateTime dt)
		{
			return new DateTime(dt.Ticks / TicksForSecond * TicksForSecond);
		}

		public static DateTime TrimBelowMinute(DateTime dt)
		{
			return new DateTime(dt.Ticks / TicksForMinute * TicksForMinute);
		}

		public static DateTime TrimBelowHour(DateTime dt)
		{
			return new DateTime(dt.Ticks / TicksForHour * TicksForHour);
		}

		public static long ToTotalSeconds(DateTime dt)
		{
			return dt.Ticks / TicksForSecond;
		}

		public static DateTime FromTotalSeconds(long seconds)
		{
			return new DateTime(seconds * TicksForSecond);
		}

		#region UNIX
		private static readonly DateTime dt1970 = new DateTime(1970, 1, 1);

		public static int ToUnixTime(DateTime dt)
		{
			return (int)(dt - dt1970).TotalSeconds;
		}

		public static DateTime FromUnixTime(int unixTime)
		{
			return dt1970 + TimeSpan.FromSeconds(unixTime);
		}
		#endregion

		public static string ToDbFormat(DateTime dt)
		{
			return dt.ToString("yyyy-MM-dd HH:mm:ss");
		}

		public static string ToRfc3339(DateTime dt)
		{
			return dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ssZ");
		}

		public static string ToRfc3339Modified(DateTime dt)
		{
			return dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ssZ");
		}

		public static DateTime FromRfc3339(string dt)
		{
			return DateTime.ParseExact(dt, "yyyy-MM-dd'T'HH:mm:ssZ", CultureInfo.InvariantCulture);
		}

		public static string ToRfc3339fff(DateTime dt)
		{
			return dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ");
		}

		public static DateTime FromDbFormat(string dt)
		{
			return DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		public static DateTime FromRfc3339fff(string dt)
		{
			return DateTime.ParseExact(dt, "yyyy-MM-dd'T'HH:mm:ss.fffZ", CultureInfo.InvariantCulture);
		}

		public static DateTime ParseYYYYMMDD(string dt)
		{
			return DateTime.ParseExact(dt, "yyyyMMdd", CultureInfo.InvariantCulture);
		}

		public static DateTime ParseYYYYMM(string dt)
		{
			return DateTime.ParseExact(dt, "yyyyMM", CultureInfo.InvariantCulture);
		}

		public static List<Tuple<DateTime, DateTime>> GetDays(DateTime start, DateTime end)
		{
			start = new DateTime(start.Year, start.Month, start.Day);
			end = new DateTime(end.Year, end.Month, end.Day).AddDays(1);

			var list = new List<Tuple<DateTime, DateTime>>();

			for (DateTime date = start; date < end; date = date.AddDays(1))
			{
				list.Add(new Tuple<DateTime, DateTime>(date, date.AddDays(1)));
			}

			return list;
		}

		public static List<Tuple<DateTime, DateTime>> GetWeeks(DateTime start, DateTime end)
		{
			start = new DateTime(start.Year, start.Month, start.Day);
			switch (start.DayOfWeek)
			{
				case DayOfWeek.Monday: start = start.AddDays(-1); break;
				case DayOfWeek.Tuesday: start = start.AddDays(-2); break;
				case DayOfWeek.Wednesday: start = start.AddDays(-3); break;
				case DayOfWeek.Thursday: start = start.AddDays(-4); break;
				case DayOfWeek.Friday: start = start.AddDays(-5); break;
				case DayOfWeek.Saturday: start = start.AddDays(-6); break;
			}

			end = new DateTime(end.Year, end.Month, end.Day);
			switch (end.DayOfWeek)
			{
				case DayOfWeek.Sunday: end = end.AddDays(6); break;
				case DayOfWeek.Monday: end = end.AddDays(5); break;
				case DayOfWeek.Tuesday: end = end.AddDays(4); break;
				case DayOfWeek.Wednesday: end = end.AddDays(3); break;
				case DayOfWeek.Thursday: end = end.AddDays(2); break;
				case DayOfWeek.Friday: end = end.AddDays(1); break;
			}

			var list = new List<Tuple<DateTime, DateTime>>();

			for (DateTime date = start; date < end; date = date.AddDays(7))
			{
				list.Add(new Tuple<DateTime, DateTime>(date, date.AddDays(7)));
			}

			return list;
		}

		public static List<Tuple<DateTime, DateTime>> GetMonths(DateTime start, DateTime end)
		{
			start = new DateTime(start.Year, start.Month, 1);
			end = new DateTime(end.Year, end.Month, 1).AddMonths(1);

			var list = new List<Tuple<DateTime, DateTime>>();

			for (DateTime date = start; date < end; date = date.AddMonths(1))
			{
				list.Add(new Tuple<DateTime, DateTime>(date, date.AddMonths(1)));
			}

			return list;
		}

		public static bool IsSameYear(DateTime dt1, DateTime dt2)
		{
			return (dt1.Year == dt2.Year);
		}

		public static bool IsSameMonth(DateTime dt1, DateTime dt2)
		{
			return (dt1.Year == dt2.Year) && (dt1.Month == dt2.Month);
		}

		public static bool IsSameWeek(DateTime dt1, DateTime dt2)
		{
			DateTime date1 = dt1.Date;
			DateTime date2 = dt2.Date;
			DateTime week1 = date1 - TimeSpan.FromDays((int)date1.DayOfWeek);
			DateTime week2 = date2 - TimeSpan.FromDays((int)date2.DayOfWeek);
			return (week1 == week2);
		}

		public static bool IsSameDay(DateTime dt1, DateTime dt2)
		{
			return (dt1.Date == dt2.Date);
		}

		public static bool IsSameHour(DateTime dt1, DateTime dt2)
		{
			return (dt1.Date == dt2.Date) && (dt1.Hour == dt2.Hour);
		}

		public static bool IsSameMinute(DateTime dt1, DateTime dt2)
		{
			return (dt1.Ticks / TicksForMinute * TicksForMinute) == (dt2.Ticks / TicksForMinute * TicksForMinute);
		}

		public static bool IsSameSecond(DateTime dt1, DateTime dt2)
		{
			return (dt1.Ticks / TicksForSecond * TicksForSecond) == (dt2.Ticks / TicksForSecond * TicksForSecond);
		}

		public static DateTime GetNextYear(DateTime dt)
		{
			return new DateTime(dt.Year + 1, 1, 1);
		}

		public static DateTime GetNextMonth(DateTime dt)
		{
			dt = dt.AddMonths(1);
			return new DateTime(dt.Year, dt.Month, 1);
		}

		public static DateTime GetNextWeek(DateTime dt)
		{
			return dt.Date.AddDays(7 - (int)dt.DayOfWeek);
		}

		public static DateTime GetNextDay(DateTime dt)
		{
			return dt.Date.AddDays(1);
		}

		public static DateTime GetNextHour(DateTime dt)
		{
			dt = dt.AddHours(1);
			return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
		}

		public static DateTime FilterSecond(this DateTime dt)
		{
			return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
		}

		public static DateTime FilterMinute(this DateTime dt)
		{
			return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
		}
	}
}
