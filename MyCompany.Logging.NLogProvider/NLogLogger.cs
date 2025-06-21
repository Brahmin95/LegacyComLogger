using MyCompany.Logging.Abstractions;
using NLog;
using System;
using System.Collections.Generic;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// The NLog implementation of the ILogger interface. This class is the core of the provider,
    /// responsible for translating the abstract logging calls into concrete NLog events and
    /// performing enrichments.
    /// </summary>
    public class NLogLogger : Abstractions.ILogger
    {
        private readonly NLog.ILogger _nlogLogger;
        private readonly IApmAgentWrapper _apmWrapper;

        /// <summary>
        /// Initializes a new instance of the NLogLogger class.
        /// It uses constructor injection to receive its dependencies, making it highly testable.
        /// </summary>
        /// <param name="nlogLogger">The underlying NLog.ILogger instance to write to.</param>
        /// <param name="apmWrapper">An abstraction for the Elastic APM agent for testability.</param>
        public NLogLogger(NLog.ILogger nlogLogger, IApmAgentWrapper apmWrapper)
        {
            _nlogLogger = nlogLogger ?? throw new ArgumentNullException(nameof(nlogLogger));
            _apmWrapper = apmWrapper ?? throw new ArgumentNullException(nameof(apmWrapper));
        }

        #region .NET Consumer Methods
        /// <inheritdoc/>
        public void Trace(string mt, params object[] a) => _nlogLogger.Trace(mt, a);
        /// <inheritdoc/>
        public void Debug(string mt, params object[] a) => _nlogLogger.Debug(mt, a);
        /// <inheritdoc/>
        public void Info(string mt, params object[] a) => _nlogLogger.Info(mt, a);
        /// <inheritdoc/>
        public void Warn(string mt, params object[] a) => _nlogLogger.Warn(mt, a);
        /// <inheritdoc/>
        public void Error(Exception exception, string mt, params object[] a) => _nlogLogger.Error(exception, mt, a);
        /// <inheritdoc/>
        public void Fatal(Exception exception, string mt, params object[] a) => _nlogLogger.Fatal(exception, mt, a);
        #endregion

        #region VB6 Consumer Methods
        /// <inheritdoc/>
        public void Trace(string m, Dictionary<string, object> p = null) => Log(LogLevel.Trace, m, null, p);
        /// <inheritdoc/>
        public void Debug(string m, Dictionary<string, object> p = null) => Log(LogLevel.Debug, m, null, p);
        /// <inheritdoc/>
        public void Info(string m, Dictionary<string, object> p = null) => Log(LogLevel.Info, m, null, p);
        /// <inheritdoc/>
        public void Warn(string m, Dictionary<string, object> p = null) => Log(LogLevel.Warn, m, null, p);
        /// <inheritdoc/>
        public void Error(string m, Exception exception = null, Dictionary<string, object> p = null) => Log(LogLevel.Error, m, exception, p);
        /// <inheritdoc/>
        public void Fatal(string m, Exception exception = null, Dictionary<string, object> p = null) => Log(LogLevel.Fatal, m, exception, p);
        #endregion

        /// <summary>
        /// The core logging method for dictionary-based calls. It builds a LogEventInfo object,
        /// performs all enrichments, and passes it to the underlying NLog logger.
        /// </summary>
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

            // ENRICHMENT PHASE 2: Elastic APM Correlation IDs
            // This logic now calls the testable wrapper instead of the static Agent.
            var traceId = _apmWrapper.GetCurrentTraceId();
            if (!string.IsNullOrEmpty(traceId)) { logEvent.Properties["trace.id"] = traceId; }

            var transactionId = _apmWrapper.GetCurrentTransactionId();
            if (!string.IsNullOrEmpty(transactionId)) { logEvent.Properties["transaction.id"] = transactionId; }

            var spanId = _apmWrapper.GetCurrentSpanId();
            if (!string.IsNullOrEmpty(spanId)) { logEvent.Properties["span.id"] = spanId; }

            // ENRICHMENT PHASE 3: Deeper APM Integration
            // If logging an Error or Fatal, add the rich context directly to the APM transaction.
            if (level >= LogLevel.Error && mutableProperties.Count > 0)
            {
                _apmWrapper.AddCustomContext(mutableProperties);
            }

            // FINALIZATION: Add all remaining custom properties to the log event.
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