using System;
using Newtonsoft.Json;

namespace Cloud9.Omg.Connector.Support
{
    public sealed class ConsoleConnectorLog : IConnectorLog
    {
        public void Info(string message)
        {
            Write("information", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("error", message, exception == null ? null : exception.GetType().Name + ": " + exception.Message);
        }

        private static void Write(string level, string message, string error)
        {
            Console.WriteLine(JsonConvert.SerializeObject(new
            {
                timestamp = DateTimeOffset.UtcNow,
                level,
                message,
                error
            }));
        }
    }
}
