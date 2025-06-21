using MyCompany.Logging.Abstractions;
using System;
using NLog.Common;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// An implementation of IInternalLogger that forwards messages to NLog's own
    /// internal logger. This is used for diagnosing issues within the NLog provider itself.
    /// </summary>
    public class NLogInternalLogger : IInternalLogger
    {
        /// <inheritdoc/>
        public void Trace(string message, Exception exception = null)
        {
            if (exception != null) InternalLogger.Trace(exception, message);
            else InternalLogger.Trace(message);
        }
        /// <inheritdoc/>
        public void Debug(string message, Exception exception = null)
        {
            if (exception != null) InternalLogger.Debug(exception, message);
            else InternalLogger.Debug(message);
        }
        /// <inheritdoc/>
        public void Info(string message, Exception exception = null)
        {
            if (exception != null) InternalLogger.Info(exception, message);
            else InternalLogger.Info(message);
        }
        /// <inheritdoc/>
        public void Warn(string message, Exception exception = null)
        {
            if (exception != null) InternalLogger.Warn(exception, message);
            else InternalLogger.Warn(message);
        }
        /// <inheritdoc/>
        public void Error(string message, Exception exception = null)
        {
            if (exception != null) InternalLogger.Error(exception, message);
            else InternalLogger.Error(message);
        }
        /// <inheritdoc/>
        public void Fatal(string message, Exception exception = null)
        {
            if (exception != null) InternalLogger.Fatal(exception, message);
            else InternalLogger.Fatal(message);
        }
    }
}