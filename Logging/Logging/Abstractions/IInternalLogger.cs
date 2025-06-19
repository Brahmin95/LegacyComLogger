using System;

namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines a complete contract for logging the internal state and errors
    /// of the logging framework's components themselves.
    /// </summary>
    public interface IInternalLogger
    {
        // Add all relevant log levels
        void Trace(string message, Exception exception = null);
        void Debug(string message, Exception exception = null);
        void Info(string message, Exception exception = null);
        void Warn(string message, Exception exception = null);
        void Error(string message, Exception exception = null);
        void Fatal(string message, Exception exception = null);
    }
}