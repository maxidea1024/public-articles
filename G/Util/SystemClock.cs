using System;

namespace G.Util
{
    /// <summary>
    /// Provides the function to inquire the system clock value. It is thread safe.
    /// </summary>
    public static class SystemClock
    {
        #region Constant values

        /// <summary>
        /// A constant value corresponding to the number of ticks per second.
        /// </summary>
        public const long TicksPerSecond = TimeSpan.TicksPerSecond;

        /// <summary>
        /// A constant value corresponding to the number of ticks per millisecond.
        /// </summary>
        public const long TicksPerMillisecond = TimeSpan.TicksPerMillisecond;

        #endregion

        #region Properties

        /// <summary>
        /// The total accumulated ticks for the current time in UTC.
        /// </summary>
        public static long Ticks => DateTime.UtcNow.Ticks;

        /// <summary>
        /// The total accumulated milliseconds for the current time in UTC.In other words,
        /// it is a value representing the current time in milliseconds.
        /// </summary>
        public static long Milliseconds => DateTime.UtcNow.Ticks / TicksPerMillisecond;

        /// <summary>
        /// The total accumulated seconds for the current time in UTC.In other words,
        /// it is a value representing the current time in seconds.
        /// </summary>
        public static double Seconds => (double) DateTime.UtcNow.Ticks / TicksPerSecond;

        #endregion

        #region Helpers

        /// <summary>
        /// Converts from milliseconds to ticks units
        /// </summary>
        public static long FromMilliseconds(double milliseconds) => (long) (milliseconds * TicksPerMillisecond);

        /// <summary>
        /// Converts from second tos ticks units
        /// </summary>
        public static long FromSeconds(double seconds) => (long) (seconds * TicksPerSecond);

        /// <summary>
        /// Converts from ticks to seconds units
        /// </summary>
        public static double ToMilliseconds(long ticks) => (double) ticks / TicksPerMillisecond;

        /// <summary>
        /// Converts from ticks to milliseconds units
        /// </summary>
        public static double ToSeconds(long ticks) => (double) ticks / TicksPerSecond;

        #endregion
    }
}
