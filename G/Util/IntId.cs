using System.Runtime.InteropServices;

namespace G.Util
{
	[StructLayout(LayoutKind.Explicit)]
	public struct IntId
	{
		public static readonly IntId None = new IntId(-1);

		[FieldOffset(0)] public int Id;
		[FieldOffset(0)] public short Index;
		[FieldOffset(2)] public short Seq;

		public IntId(IntId id)
		{
			Index = Seq = -1;
			Id = id.Id;
		}

		public IntId(int id)
		{
			Index = Seq = -1;
			Id = id;
		}

		public IntId(short index, short seq)
		{
			Id = -1;
			Index = index;
			Seq = seq;
		}

		public void Refresh()
		{
			if (Seq < short.MaxValue)
				Seq++;
			else
				Seq = 0;
		}

		public static bool operator ==(IntId left, IntId right)
		{
			return (left.Id == right.Id);
		}

		public static bool operator !=(IntId left, IntId right)
		{
			return (left.Id != right.Id);
		}

		public override bool Equals(object obj)
		{
			if (obj is IntId)
			{
				return (Id == ((IntId)obj).Id);
			}
			return false;
		}

		public static explicit operator IntId(int id)
		{
			return new IntId(id);
		}

		public static implicit operator int(IntId intId)
		{
			return intId.Id;
		}

		public override int GetHashCode()
		{
			return Id;
		}

		public override string ToString()
		{
			return Id.ToString();
		}
	}
}
