using System;

namespace G.Network
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ProtocolHandlerAttribute : Attribute
    {
        public string ProtocolNamespace { get; private set; }

        public ProtocolHandlerAttribute(string protocolNamespace = null)
        {
            ProtocolNamespace = protocolNamespace;
        }
    }
}
