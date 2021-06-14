using System;

namespace G.Network
{
	public class InvalidProtocolException : Exception
	{
		public InvalidProtocolException() { }
		public InvalidProtocolException(string message) : base(message) { }
	}
}
