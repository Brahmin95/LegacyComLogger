using MyCompany.Logging.Abstractions;
using NLog;
using System;
using System.Collections.Generic;
using Elastic.Apm;

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
        public void Error(Exception exception, string mt, params object[] a) => _nlogLogger.Error(exception, mt, a);
        public void Fatal(Exception exception, string mt, params object[] a) => _nlogLogger.Fatal(exception, mt, a);
        public void Trace(string m, Dictionary<string, object> p = null) => Log(LogLevel.Trace, m, null, p);
        public void Debug(string m, Dictionary<string, object> p = null) => Log(LogLevel.Debug, m, null, p);
        public void Info(string m, Dictionary<string, object> p = null) => Log(LogLevel.Info, m, null, p);
        public void Warn(string m, Dictionary<string, object> p = null) => Log(LogLevel.Warn, m, null, p);
        public void Error(string m, Exception exception = null, Dictionary<string, object> p = null) => Log(LogLevel.Error, m, exception, p);
        public void Fatal(string m, Exception exception = null, Dictionary<string, object> p = null) => Log(LogLevel.Fatal, m, exception, p);


        private void Log(LogLevel level, string message, Exception ex, Dictionary<string, object> properties)
        {
            if (!_nlogLogger.IsEnabled(level)) return;

            var logEvent = new LogEventInfo(level, _nlogLogger.Name, message) { Exception = ex };

            // Create a mutable dictionary of properties to work with.
            var mutableProperties = properties != null ? new Dictionary<string, object>(properties) : new Dictionary<string, object>();

            // =======================================================
            // ENRICHMENT PHASE 1: VB6 Call Site Information (CORRECTED)
            // =======================================================
            // We can't set the read-only Caller... properties.
            // Instead, we add special properties to the LogEvent's Properties dictionary.
            // The ecs-layout and other layouts know how to find and use these special keys.

            if (mutableProperties.TryGetValue("vbCodeFile", out object codeFile) && codeFile is string)
            {
                // Add the file name using the special 'callsite-filename' key.
                logEvent.Properties["callsite-filename"] = (string)codeFile;
                mutableProperties.Remove("vbCodeFile"); // Clean up the temporary property
            }
            if (mutableProperties.TryGetValue("vbMethodName", out object methodName) && methodName is string)
            {
                // Add the method name using the special 'callsite' key.
                logEvent.Properties["callsite"] = (string)methodName;
                mutableProperties.Remove("vbMethodName"); // Clean up the temporary property
            }

            // =======================================================
            // ENRICHMENT PHASE 2: Elastic APM Correlation IDs
            // =======================================================
            if (Agent.IsConfigured && Agent.Tracer.CurrentTransaction != null)
            {
                var currentTransaction = Agent.Tracer.CurrentTransaction;
                logEvent.Properties["transaction.id"] = currentTransaction.Id;
                logEvent.Properties["trace.id"] = currentTransaction.TraceId;
                if (Agent.Tracer.CurrentSpan != null && Agent.Tracer.CurrentSpan.Id != currentTransaction.Id)
                {
                    logEvent.Properties["span.id"] = Agent.Tracer.CurrentSpan.Id;
                }
            }

            // =======================================================
            // FINALIZATION: Add all remaining properties to the event.
            // =================================G======================
            foreach (var prop in mutableProperties)
            {
                if (!logEvent.Properties.ContainsKey(prop.Key))
                {
                    logEvent.Properties[prop.Key] = prop.Value;
                }
            }

            _nlogLogger.Log(logEvent);
        }
    }
}

