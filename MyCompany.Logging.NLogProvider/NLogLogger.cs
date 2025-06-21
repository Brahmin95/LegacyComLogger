using MyCompany.Logging.Abstractions;
using NLog;
using System;
using System.Collections.Generic;

namespace MyCompany.Logging.NLogProvider
{
    public class NLogLogger : Abstractions.ILogger
    {
        private readonly NLog.ILogger _nlogLogger;
        private readonly IApmAgentWrapper _apmWrapper;

        // MODIFIED: Constructor now accepts the IApmAgentWrapper dependency.
        public NLogLogger(NLog.ILogger nlogLogger, IApmAgentWrapper apmWrapper)
        {
            _nlogLogger = nlogLogger ?? throw new ArgumentNullException(nameof(nlogLogger));
            _apmWrapper = apmWrapper ?? throw new ArgumentNullException(nameof(apmWrapper));
        }

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

            var mutableProperties = properties != null ? new Dictionary<string, object>(properties) : new Dictionary<string, object>();

            // ENRICHMENT PHASE 1: VB6 Call Site Information
            if (mutableProperties.TryGetValue("vbCodeFile", out object codeFile) && codeFile is string)
            {
                logEvent.Properties["callsite-filename"] = (string)codeFile;
                mutableProperties.Remove("vbCodeFile");
            }
            if (mutableProperties.TryGetValue("vbMethodName", out object methodName) && methodName is string)
            {
                logEvent.Properties["callsite"] = (string)methodName;
                mutableProperties.Remove("vbMethodName");
            }

            // =======================================================
            // ENRICHMENT PHASE 2: Elastic APM Correlation IDs (REFACTORED)
            // =======================================================
            // Logic now calls the testable wrapper instead of the static Agent.
            var traceId = _apmWrapper.GetCurrentTraceId();
            if (!string.IsNullOrEmpty(traceId))
            {
                logEvent.Properties["trace.id"] = traceId;
            }
            var transactionId = _apmWrapper.GetCurrentTransactionId();
            if (!string.IsNullOrEmpty(transactionId))
            {
                logEvent.Properties["transaction.id"] = transactionId;
            }
            var spanId = _apmWrapper.GetCurrentSpanId();
            if (!string.IsNullOrEmpty(spanId))
            {
                logEvent.Properties["span.id"] = spanId;
            }

            // FINALIZATION: Add all remaining properties to the event.
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