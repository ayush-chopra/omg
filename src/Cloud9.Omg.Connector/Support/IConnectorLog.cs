using System;

namespace Cloud9.Omg.Connector.Support
{
    public interface IConnectorLog
    {
        void Info(string message);
        void Error(string message, Exception exception);
    }
}
