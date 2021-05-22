using System;

namespace G.Util
{
	public class SectionInt
	{
		public int Min { get; private set; }
		public int Max { get; private set; }

		public SectionInt(int center, int range) : this(center, range, -range)
		{
		}

		public SectionInt(int center, int range1, int range2)
		{
			Min = center + range1;
			Max = center + range2;

			if (Min > Max)
			{
				int tmp = Min;
				Min = Max;
				Max = tmp;
			}
		}

		public SectionInt(int center, float ratio) : this(center, (int)(center * ratio))
		{
		}

		public SectionInt(int center, float ratio1, float ratio2) : this(center, (int)(center * ratio1), (int)(center * ratio2))
		{
		}

		public bool IsIntersected(int n)
		{
			return (n >= Min && n <= Max);
		}

		public bool IsIntersected(SectionInt section)
		{
			if (IsIntersected(section.Min)) return true;
			if (IsIntersected(section.Max)) return true;
			return false;
		}

		public override string ToString()
		{
			return $"({Min}, {Max})";
		}
	}

	public class SectionFloat
	{
		public float Min { get; private set; }
		public float Max { get; private set; }

		public SectionFloat(float center, float ratio) : this(center, ratio, -ratio)
		{
		}

		public SectionFloat(float center, float ratio1, float ratio2)
		{
			float range1 = center * ratio1;
			float range2 = center * ratio2;

			Min = center + range1;
			Max = center + range2;

			if (Min > Max)
			{
				float tmp = Min;
				Min = Max;
				Max = tmp;
			}
		}

		public bool IsIntersected(float n)
		{
			return (n >= Min && n <= Max);
		}

		public bool IsIntersected(SectionFloat section)
		{
			if (IsIntersected(section.Min)) return true;
			if (IsIntersected(section.Max)) return true;
			return false;
		}

		public override string ToString()
		{
			return $"({Min}, {Max})";
		}
	}

	public class SectionDouble
	{
		public double Min { get; private set; }
		public double Max { get; private set; }

		public SectionDouble(double center, double ratio) : this(center, ratio, -ratio)
		{
		}

		public SectionDouble(double center, double ratio1, double ratio2)
		{
			double range1 = center * ratio1;
			double range2 = center * ratio2;

			Min = center + range1;
			Max = center + range2;

			if (Min > Max)
			{
				double tmp = Min;
				Min = Max;
				Max = tmp;
			}
		}

		public bool IsIntersected(double n)
		{
			return (n >= Min && n <= Max);
		}

		public bool IsIntersected(SectionDouble section)
		{
			if (IsIntersected(section.Min)) return true;
			if (IsIntersected(section.Max)) return true;
			return false;
		}

		public override string ToString()
		{
			return $"({Min}, {Max})";
		}
	}

	public class SectionTime
	{
		public DateTime Min { get; private set; }
		public DateTime Max { get; private set; }

		public int Range
		{
			get
			{
				if (Max < Min) return 0;
				return (Max - Min).Days + 1;
			}
		}

		public SectionTime(DateTime min, DateTime max)
		{
			if (min > max)
			{
				Min = max;
				Max = min;
			}
			else
			{
				Min = min;
				Max = max;
			}
		}

		public bool IsIntersected(DateTime dt)
		{
			return (dt >= Min && dt <= Max);
		}

		public bool IsIntersected(SectionTime section)
		{
			if (IsIntersected(section.Min)) return true;
			if (IsIntersected(section.Max)) return true;
			return false;
		}

		public SectionTime Intersect(SectionTime section)
		{
			DateTime min = Min;
			if (section.Min > min) min = section.Min;

			DateTime max = Max;
			if (section.Max < max) max = section.Max;

			if (min > max)
				return null;
			else
				return new SectionTime(min, max);
		}
	}
}
