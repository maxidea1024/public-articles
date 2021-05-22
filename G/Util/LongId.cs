using System.Runtime.InteropServices;

namespace G.Util
{
	[StructLayout(LayoutKind.Explicit)]
	public struct LongId
	{
		public static readonly LongId None = new LongId(-1);

		[FieldOffset(0)] public long Id;
		[FieldOffset(0)] public int Index;
		[FieldOffset(4)] public int Seq;

		public LongId(LongId id)
		{
			Index = Seq = -1;
			Id = id.Id;
		}

		public LongId(long id)
		{
			Index = Seq = -1;
			Id = id;
		}

		public LongId(int index, int seq)
		{
			Id = -1;
			Index = index;
			Seq = seq;
		}

		public void Refresh()
		{
			if (Seq < int.MaxValue)
				Seq++;
			else
				Seq = 0;
		}

		public static bool operator ==(LongId left, LongId right)
		{
			return (left.Id == right.Id);
		}

		public static bool operator !=(LongId left, LongId right)
		{
			return (left.Id != right.Id);
		}

		public override bool Equals(object obj)
		{
			if (obj is LongId)
			{
				return (Id == ((LongId)obj).Id);
			}
			return false;
		}

		public static explicit operator LongId(long id)
		{
			return new LongId(id);
		}

		public static implicit operator long(LongId longId)
		{
			return longId.Id;
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public override string ToString()
		{
			return Id.ToString();
		}
	}
}
