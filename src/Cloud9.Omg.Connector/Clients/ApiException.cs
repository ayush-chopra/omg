using System;

namespace Cloud9.Omg.Connector.Clients
{
    public sealed class ApiException : Exception
    {
        public ApiException(string message)
            : base(message)
        {
        }

        public ApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
