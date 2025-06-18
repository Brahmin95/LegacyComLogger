using System;
using System.Collections.Generic;

namespace MyCompany.Logging.Abstractions
{
    public interface ILogger
    {
        void Trace(string messageTemplate, params object[] args);
        void Debug(string messageTemplate, params object[] args);
        void Info(string messageTemplate, params object[] args);
        void Warn(string messageTemplate, params object[] args);
        void Error(Exception ex, string messageTemplate, params object[] args);
        void Fatal(Exception ex, string messageTemplate, params object[] args);

        void Trace(string message, Dictionary<string, object> properties = null);
        void Debug(string message, Dictionary<string, object> properties = null);
        void Info(string message, Dictionary<string, object> properties = null);
        void Warn(string message, Dictionary<string, object> properties = null);
        void Error(string message, Exception ex = null, Dictionary<string, object> properties = null);
        void Fatal(string message, Exception ex = null, Dictionary<string, object> properties = null);
    }
}