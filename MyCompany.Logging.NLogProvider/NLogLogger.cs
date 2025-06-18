using MyCompany.Logging.Abstractions;
using NLog;
using System;
using System.Collections.Generic;

namespace MyCompany.Logging.NLogProvider
{
    public class NLogLogger : Abstractions.ILogger
    {
        private readonly NLog.ILogger _nlogLogger;
        public NLogLogger(NLog.ILogger nlogLogger) { _nlogLogger = nlogLogger; }

        public void Trace(string mt, params object[] a) => _nlogLogger.Trace(mt, a);
        public void Debug(string mt, params object[] a) => _nlogLogger.Debug(mt, a);
        public void Info(string mt, params object[] a) => _nlogLogger.Info(mt, a);
        public void Warn(string mt, params object[] a) => _nlogLogger.Warn(mt, a);
        public void Error(Exception ex, string mt, params object[] a) => _nlogLogger.Error(ex, mt, a);
        public void Fatal(Exception ex, string mt, params object[] a) => _nlogLogger.Fatal(ex, mt, a);

        public void Trace(string m, Dictionary<string, object> p = null) => Log(LogLevel.Trace, m, null, p);
        public void Debug(string m, Dictionary<string, object> p = null) => Log(LogLevel.Debug, m, null, p);
        public void Info(string m, Dictionary<string, object> p = null) => Log(LogLevel.Info, m, null, p);
        public void Warn(string m, Dictionary<string, object> p = null) => Log(LogLevel.Warn, m, null, p);
        public void Error(string m, Exception ex = null, Dictionary<string, object> p = null) => Log(LogLevel.Error, m, ex, p);
        public void Fatal(string m, Exception ex = null, Dictionary<string, object> p = null) => Log(LogLevel.Fatal, m, ex, p);

        private void Log(LogLevel level, string message, Exception ex, Dictionary<string, object> properties)
        {
            if (!_nlogLogger.IsEnabled(level)) return;
            var logEvent = new LogEventInfo(level, _nlogLogger.Name, message) { Exception = ex };
            if (properties != null)
            {
                foreach (var prop in properties) { logEvent.Properties[prop.Key] = prop.Value; }
            }
            _nlogLogger.Log(logEvent);
        }
    }
}