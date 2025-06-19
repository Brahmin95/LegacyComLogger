using MyCompany.Logging.Abstractions;
using System;
using NLog.Common; // The namespace for NLog's InternalLogger

namespace MyCompany.Logging.NLogProvider
{
    public class NLogInternalLogger : IInternalLogger
    {
        public void Trace(string message, Exception exception = null)
        {
            if (exception != null)
                InternalLogger.Trace(exception, message);
            else
                InternalLogger.Trace(message);
        }

        public void Debug(string message, Exception exception = null)
        {
            if (exception != null)
                InternalLogger.Debug(exception, message);
            else
                InternalLogger.Debug(message);
        }

        public void Info(string message, Exception exception = null)
        {
            if (exception != null)
                InternalLogger.Info(exception, message);
            else
                InternalLogger.Info(message);
        }

        public void Warn(string message, Exception exception = null)
        {
            if (exception != null)
                InternalLogger.Warn(exception, message);
            else
                InternalLogger.Warn(message);
        }

        public void Error(string message, Exception exception = null)
        {
            if (exception != null)
                InternalLogger.Error(exception, message);
            else
                InternalLogger.Error(message);
        }

        public void Fatal(string message, Exception exception = null)
        {
            if (exception != null)
                InternalLogger.Fatal(exception, message);
            else
                InternalLogger.Fatal(message);
        }
    }
}