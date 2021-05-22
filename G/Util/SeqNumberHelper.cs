using System.Runtime.CompilerServices;

namespace G.Util
{
    /// <summary>
    /// SeqNumberHelper16
    /// </summary>
    public static class SeqNumberHelper16
    {
        /// <summary>x < y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Less(ushort x, ushort y)
        {
            return (short)(y - x) > 0;
        }

        /// <summary>x <= y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LessOrEqual(ushort x, ushort y)
        {
            return x == y || Less(x, y);
        }

        /// <summary>x > y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Greater(ushort x, ushort y)
        {
            return !LessOrEqual(x, y);
        }

        /// <summary>x >= y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GreaterOrEqual(ushort x, ushort y)
        {
            return x == y || !Less(x, y);
        }

        /// <summary>Compares two seq numbers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(ushort x, ushort y)
        {
            return (x != y) ? (Less(x, y) ? -1 : +1) : 0;
        }
    }

    /// <summary>
    /// SeqNumberHelper32
    /// </summary>
    public static class SeqNumberHelper32
    {
        /// <summary>x < y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Less(uint x, uint y)
        {
            return (int)(y - x) > 0;
        }

        /// <summary>x <= y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LessOrEqual(uint x, uint y)
        {
            return x == y || Less(x, y);
        }

        /// <summary>x > y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Greater(uint x, uint y)
        {
            return !LessOrEqual(x, y);
        }

        /// <summary>x >= y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GreaterOrEqual(uint x, uint y)
        {
            return x == y || !Less(x, y);
        }

        /// <summary>Compares two seq numbers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(uint x, uint y)
        {
            return (x != y) ? (Less(x, y) ? -1 : +1) : 0;
        }
    }

    /// <summary>
    /// SeqNumberHelper64
    /// </summary>
    public static class SeqNumberHelper64
    {
        /// <summary>x < y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Less(ulong x, ulong y)
        {
            return (long)(y - x) > 0;
        }

        /// <summary>x <= y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LessOrEqual(ulong x, ulong y)
        {
            return x == y || Less(x, y);
        }

        /// <summary>x > y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Greater(ulong x, ulong y)
        {
            return !LessOrEqual(x, y);
        }

        /// <summary>x >= y</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GreaterOrEqual(ulong x, ulong y)
        {
            return x == y || !Less(x, y);
        }

        /// <summary>Compares two seq numbers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(ulong x, ulong y)
        {
            return (x != y) ? (Less(x, y) ? -1 : +1) : 0;
        }
    }
}
