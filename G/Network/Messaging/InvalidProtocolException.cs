using System;

namespace G.Network.Messaging
{
	public class InvalidProtocolException : Exception
	{
		public InvalidProtocolException() { }
		public InvalidProtocolException(string message) : base(message) { }
	}
}
