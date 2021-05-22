namespace G.Util
{
	public class InterlockedFlag
	{
		private bool value;
		public bool Value { get { return value; } }

		public InterlockedFlag()
		{
		}

		public InterlockedFlag(bool initialValue)
		{
			value = initialValue;
		}

		public bool Set()
		{
			lock (this)
			{
				if (value == true) return false;
				value = true;
				return true;
			}
		}

		public bool Reset()
		{
			lock (this)
			{
				if (value == false) return false;
				value = false;
				return true;
			}
		}
	}
}
