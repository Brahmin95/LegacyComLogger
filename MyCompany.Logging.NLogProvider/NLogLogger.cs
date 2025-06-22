using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
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
        // These methods are now rewritten to funnel through the common enrichment pipeline.

        /// <inheritdoc/>
        public void Trace(string messageTemplate, params object[] args)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Trace)) return;
            var logEvent = new LogEventInfo(LogLevel.Trace, _nlogLogger.Name, null, messageTemplate, args);
            EnrichAndLog(logEvent, null);
        }

        /// <inheritdoc/>
        public void Debug(string messageTemplate, params object[] args)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Debug)) return;
            var logEvent = new LogEventInfo(LogLevel.Debug, _nlogLogger.Name, null, messageTemplate, args);
            EnrichAndLog(logEvent, null);
        }

        /// <inheritdoc/>
        public void Info(string messageTemplate, params object[] args)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Info)) return;
            var logEvent = new LogEventInfo(LogLevel.Info, _nlogLogger.Name, null, messageTemplate, args);
            EnrichAndLog(logEvent, null);
        }

        /// <inheritdoc/>
        public void Warn(string messageTemplate, params object[] args)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Warn)) return;
            var logEvent = new LogEventInfo(LogLevel.Warn, _nlogLogger.Name, null, messageTemplate, args);
            EnrichAndLog(logEvent, null);
        }

        /// <inheritdoc/>
        public void Error(Exception ex, string messageTemplate, params object[] args)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Error)) return;
            var logEvent = new LogEventInfo(LogLevel.Error, _nlogLogger.Name, null, messageTemplate, args) { Exception = ex };
            EnrichAndLog(logEvent, null);
        }

        /// <inheritdoc/>
        public void Fatal(Exception ex, string messageTemplate, params object[] args)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Fatal)) return;
            var logEvent = new LogEventInfo(LogLevel.Fatal, _nlogLogger.Name, null, messageTemplate, args) { Exception = ex };
            EnrichAndLog(logEvent, null);
        }
        #endregion

        #region VB6 Consumer Methods
        // These methods are also rewritten to funnel through the same common enrichment pipeline.

        /// <inheritdoc/>
        public void Trace(string message, Dictionary<string, object> properties = null)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Trace)) return;
            var logEvent = new LogEventInfo(LogLevel.Trace, _nlogLogger.Name, message);
            EnrichAndLog(logEvent, properties);
        }

        /// <inheritdoc/>
        public void Debug(string message, Dictionary<string, object> properties = null)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Debug)) return;
            var logEvent = new LogEventInfo(LogLevel.Debug, _nlogLogger.Name, message);
            EnrichAndLog(logEvent, properties);
        }

        /// <inheritdoc/>
        public void Info(string message, Dictionary<string, object> properties = null)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Info)) return;
            var logEvent = new LogEventInfo(LogLevel.Info, _nlogLogger.Name, message);
            EnrichAndLog(logEvent, properties);
        }

        /// <inheritdoc/>
        public void Warn(string message, Dictionary<string, object> properties = null)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Warn)) return;
            var logEvent = new LogEventInfo(LogLevel.Warn, _nlogLogger.Name, message);
            EnrichAndLog(logEvent, properties);
        }

        /// <inheritdoc/>
        public void Error(string message, Exception ex = null, Dictionary<string, object> properties = null)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Error)) return;
            var logEvent = new LogEventInfo(LogLevel.Error, _nlogLogger.Name, message) { Exception = ex };
            EnrichAndLog(logEvent, properties);
        }

        /// <inheritdoc/>
        public void Fatal(string message, Exception ex = null, Dictionary<string, object> properties = null)
        {
            if (!_nlogLogger.IsEnabled(LogLevel.Fatal)) return;
            var logEvent = new LogEventInfo(LogLevel.Fatal, _nlogLogger.Name, message) { Exception = ex };
            EnrichAndLog(logEvent, properties);
        }
        #endregion

        /// <summary>
        /// The single, common pipeline for enriching and logging all events, regardless of origin.
        /// </summary>
        private void EnrichAndLog(LogEventInfo logEvent, Dictionary<string, object> properties)
        {
            var mutableProperties = properties != null ? new Dictionary<string, object>(properties) : new Dictionary<string, object>();

            // Phase 1: VB6 Call Site Information
            if (mutableProperties.TryGetValue("vbCodeFile", out object codeFile))
            {
                logEvent.Properties["callsite-filename"] = codeFile;
                mutableProperties.Remove("vbCodeFile");
            }
            if (mutableProperties.TryGetValue("vbMethodName", out object methodName))
            {
                logEvent.Properties["callsite"] = methodName;
                mutableProperties.Remove("vbMethodName");
            }

            // Phase 2: Structured VB6 Error Details
            if (logEvent.Exception is VBErrorException vbEx)
            {
                var vbErrorContext = new Dictionary<string, object>
                {
                    { "number", vbEx.VbErrorNumber },
                    { "source", vbEx.VbErrorSource }
                };
                logEvent.Properties["vb_error"] = vbErrorContext;
                if (vbEx.VbLineNumber.HasValue)
                {
                    logEvent.Properties["source.line"] = vbEx.VbLineNumber.Value;
                }
            }

            // Phase 3: Elastic APM Correlation
            var traceId = _apmWrapper.GetCurrentTraceId();
            if (!string.IsNullOrEmpty(traceId)) logEvent.Properties["trace.id"] = traceId;
            var transactionId = _apmWrapper.GetCurrentTransactionId();
            if (!string.IsNullOrEmpty(transactionId)) logEvent.Properties["transaction.id"] = transactionId;
            var spanId = _apmWrapper.GetCurrentSpanId();
            if (!string.IsNullOrEmpty(spanId)) logEvent.Properties["span.id"] = spanId;

            // Phase 4: Deeper APM Integration
            if (logEvent.Level >= LogLevel.Error && mutableProperties.Count > 0)
            {
                _apmWrapper.AddCustomContext(mutableProperties);
            }

            // FINALIZATION: Add all remaining custom properties.
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