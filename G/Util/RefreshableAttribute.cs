using System;

namespace G.Util
{
	[AttributeUsage(AttributeTargets.Class)]
	public class RefreshableAttribute : Attribute
	{
	}

	public class RefreshableMaintenance : Attribute
	{
	}
}