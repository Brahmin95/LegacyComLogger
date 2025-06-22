using Microsoft.CSharp.RuntimeBinder;
using MyCompany.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MyCompany.Logging.Interop
{
    /// <summary>
    /// A private helper class to hold the scope data for a single trace or span.
    /// </summary>
    internal class VbScope
    {
        public string TraceId { get; }
        public string SpanId { get; }
        // We link the scope to its transaction object instance for robust disposal checks.
        public LoggingTransaction Owner { get; set; }

        public VbScope(string traceId, string spanId)
        {
            TraceId = traceId;
            SpanId = spanId;
        }
    }

    /// <summary>
    /// The concrete implementation of the COM-visible logging bridge. This class acts as a
    /// scope manager and an adapter between the COM world (VB6) and the .NET logging abstraction.
    /// </summary>
    [ComVisible(true)]
    [Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("MyCompany.Logging.Interop.LoggingComBridge")]
    public class LoggingComBridge : ILoggingComBridge
    {
        // The "Backpack": A thread-static stack to hold the hierarchy of scopes for each thread.
        [ThreadStatic]
        private static Stack<VbScope> _scopeStack;
        private static Stack<VbScope> ScopeStack => _scopeStack ?? (_scopeStack = new Stack<VbScope>());

        /// <summary>
        /// Initializes a new instance of the LoggingComBridge class.
        /// The constructor ensures that the central LogManager is initialized if it hasn't been already.
        /// </summary>
        public LoggingComBridge()
        {
            if (!LogManager.IsInitialized)
            {
                LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.Vb6);
            }
        }

        #region Public API for Trace and Span Management

        /// <inheritdoc/>
        public object CreateProperties()
        {
            try
            {
                return Activator.CreateInstance(Type.GetTypeFromProgID("Scripting.Dictionary"));
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Error("Failed to create Scripting.Dictionary. The 'Microsoft Scripting Runtime' may not be registered.", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public ILoggingTransaction BeginTrace(string transactionName, TxType transactionType)
        {
            // RESILIENCE: If a trace is already active, treat this as a new top-level span
            // to prevent creating an invalid nested trace.
            if (ScopeStack.Count > 0)
            {
                LogManager.InternalLogger.Debug($"BeginTrace called while trace '{ScopeStack.First().TraceId}' is active. Creating a child span instead.");
                return BeginSpan(transactionName, transactionType);
            }

            var traceId = Guid.NewGuid().ToString("N");
            // A trace's root span has the same ID as the trace itself. This is the transaction ID.
            var scope = new VbScope(traceId, traceId);

            // Create the disposable handle and give it a callback that knows how to dispose this specific scope.
            var transaction = new LoggingTransaction(() => PopScope(scope));
            scope.Owner = transaction; // Link the scope to its owner transaction object.

            ScopeStack.Push(scope);
            Info(transactionType.ToString(), transactionName, $"Trace '{transactionName}' started.", null);
            return transaction;
        }

        /// <inheritdoc/>
        public ILoggingTransaction BeginSpan(string spanName, TxType spanType)
        {
            // RESILIENCE: If no trace is active, automatically create one to wrap this span.
            // This prevents orphaned spans and makes the API more forgiving.
            if (ScopeStack.Count == 0)
            {
                LogManager.InternalLogger.Debug("BeginSpan called with no active trace. Automatically creating a new parent trace.");
                var parentTrace = BeginTrace(spanName, spanType);
                // The developer is now responsible for disposing the automatically created parent trace.
                return parentTrace;
            }

            var parentScope = ScopeStack.Peek();
            var newSpanId = Guid.NewGuid().ToString("N");
            // The new span shares the parent's TraceId but gets its own new SpanId.
            var scope = new VbScope(parentScope.TraceId, newSpanId);

            var transaction = new LoggingTransaction(() => PopScope(scope));
            scope.Owner = transaction;

            ScopeStack.Push(scope);
            Info(spanType.ToString(), spanName, $"Span '{spanName}' started.", null);
            return transaction;
        }

        #endregion

        #region Standard Logging Methods
        /// <inheritdoc/>
        public void Trace(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Trace", codeFile, methodName, message, null, null, null, null, properties);

        /// <inheritdoc/>
        public void Debug(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Debug", codeFile, methodName, message, null, null, null, null, properties);

        /// <inheritdoc/>
        public void Info(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Info", codeFile, methodName, message, null, null, null, null, properties);

        /// <inheritdoc/>
        public void Warn(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Warn", codeFile, methodName, message, null, null, null, null, properties);

        /// <inheritdoc/>
        public void Error(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Error", codeFile, methodName, message, message, null, null, null, properties);

        /// <inheritdoc/>
        public void Fatal(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Fatal", codeFile, methodName, message, message, null, null, null, properties);

        /// <inheritdoc/>
        public void ErrorHandler(string codeFile, string methodName, string message, string errorDescription, long errorNumber, string errorSource, int lineNumber, [Optional] object properties)
            => Log("Error", codeFile, methodName, message, errorDescription, errorNumber, errorSource, lineNumber, properties);
        #endregion

        #region Private Implementation

        /// <summary>
        /// Safely pops a scope from the stack. This is the callback executed by LoggingTransaction.Dispose().
        /// </summary>
        private static void PopScope(VbScope scope)
        {
            if (ScopeStack.Count == 0) return;

            // RESILIENCE: Check for out-of-order disposal. This prevents context corruption if a developer
            // forgets to dispose a child span before its parent.
            if (ScopeStack.Peek().Owner != scope.Owner)
            {
                LogManager.InternalLogger.Warn($"Out-of-order disposal detected. A child span was likely not disposed before its parent. TraceId: {scope.TraceId}");
                return; // IMPORTANT: Do not pop the stack, leave the dangling child for later cleanup.
            }

            ScopeStack.Pop();
        }

        /// <summary>
        /// The private core logging method that all public log methods call.
        /// It now automatically enriches log events with ambient context from the scope stack.
        /// </summary>
        private void Log(string level, string codeFile, string methodName, string message, string errorDescription, long? errorNumber, string errorSource, int? lineNumber, object comProperties)
        {
            var logger = LogManager.GetLogger($"{LogManager.GetAbstractedContextProperty("service.name")}.{codeFile}");
            var finalProps = BuildProperties(codeFile, methodName, comProperties);

            // Enrich with ambient context from the stack
            if (ScopeStack.Count > 0)
            {
                var currentScope = ScopeStack.Peek();
                finalProps["trace.id"] = currentScope.TraceId;

                // The transaction.id is the ID of the root scope (bottom of the stack).
                var rootScope = ScopeStack.Last();
                finalProps["transaction.id"] = rootScope.SpanId;

                // Only add span.id if we are in a child span (i.e., not the root scope).
                if (currentScope.SpanId != rootScope.SpanId)
                {
                    finalProps["span.id"] = currentScope.SpanId;
                }
            }

            Exception exceptionForLogging = null;
            if ((level == "Error" || level == "Fatal") && !string.IsNullOrEmpty(errorDescription))
            {
                exceptionForLogging = errorNumber.HasValue
                    ? new VBErrorException(errorDescription, errorNumber.Value, errorSource, lineNumber == 0 ? (int?)null : lineNumber)
                    : new VBErrorException(errorDescription);
            }

            // Call the appropriate method on the abstract ILogger interface.
            switch (level)
            {
                case "Trace": logger.Trace(message, finalProps); break;
                case "Debug": logger.Debug(message, finalProps); break;
                case "Info": logger.Info(message, finalProps); break;
                case "Warn": logger.Warn(message, finalProps); break;
                case "Error": logger.Error(message, exceptionForLogging, finalProps); break;
                case "Fatal": logger.Fatal(message, exceptionForLogging, finalProps); break;
            }
        }

        /// <summary>
        /// Builds a .NET dictionary from the provided COM properties and enriches it
        /// with the VB6-specific call site information.
        /// </summary>
        private Dictionary<string, object> BuildProperties(string codeFile, string methodName, object comProperties)
        {
            var dict = new Dictionary<string, object> { { "vbCodeFile", codeFile }, { "vbMethodName", methodName } };
            if (comProperties != null && comProperties != Type.Missing)
            {
                var vbProps = ConvertComObjectToDictionary(comProperties);
                foreach (var prop in vbProps)
                {
                    if (!dict.ContainsKey(prop.Key))
                    {
                        dict.Add(prop.Key, prop.Value);
                    }
                }
            }
            return dict;
        }

        /// <summary>
        /// Safely converts a COM object (expected to be a Scripting.Dictionary) into a .NET Dictionary.
        /// </summary>
        private Dictionary<string, object> ConvertComObjectToDictionary(object comObject)
        {
            var dict = new Dictionary<string, object>();
            if (comObject == null) return dict;
            try
            {
                dynamic scriptDict = comObject;
                foreach (var key in scriptDict.Keys())
                {
                    dict[key.ToString()] = SanitizeValue(scriptDict.Item(key));
                }
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Warn("Failed to convert COM properties object. It may not be a Scripting.Dictionary.", ex);
            }
            return dict;
        }

        /// <summary>
        /// Sanitizes a value from a COM object before adding it to the log properties.
        /// It attempts to convert complex COM objects to a string using a "ToLogString()" convention.
        /// </summary>
        private object SanitizeValue(object value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsPrimitive || value is string || value is decimal || value is DateTime) return value;
            try
            {
                // CONVENTION: If a VB6 developer wants a custom string representation of their
                // object in the logs, they should implement a public 'ToLogString()' method.
                try { return ((dynamic)value).ToLogString(); }
                // If ToLogString() doesn't exist, we fall back to the default ToString().
                catch (RuntimeBinderException) { return value.ToString(); }
            }
            // If even ToString() fails, we return a safe placeholder.
            catch (Exception) { return "[Unsupported COM Object]"; }
        }
        #endregion
    }
}