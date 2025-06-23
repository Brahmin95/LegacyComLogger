using Microsoft.CSharp.RuntimeBinder;
using MyCompany.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
// This 'using' is now possible because we added the "Microsoft Scripting Runtime" COM reference.
using Scripting;

namespace MyCompany.Logging.Interop
{
    /// <summary>
    /// A private helper class to hold the scope data for a single trace or span.
    /// </summary>
    internal class VbScope
    {
        public string TraceId { get; }
        public string SpanId { get; }
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
        [ThreadStatic]
        private static Stack<VbScope> _scopeStack;
        private static Stack<VbScope> ScopeStack => _scopeStack ?? (_scopeStack = new Stack<VbScope>());

        public LoggingComBridge()
        {
            if (!LogManager.IsInitialized)
            {
                LogManager.Initialize(AppRuntime.Vb6);
            }
        }

        #region Public API

        public object CreateProperties()
        {
            try
            {
                return Activator.CreateInstance(Type.GetTypeFromProgID("Scripting.Dictionary"));
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Error("Failed to create Scripting.Dictionary.", ex);
                return null;
            }
        }

        public ILoggingTransaction BeginTrace(string transactionName, TxType transactionType)
        {
            if (ScopeStack.Count > 0)
            {
                return BeginSpan(transactionName, transactionType);
            }
            var traceId = Guid.NewGuid().ToString("N");
            var scope = new VbScope(traceId, traceId);
            var transaction = new LoggingTransaction(() => PopScope(scope));
            scope.Owner = transaction;
            ScopeStack.Push(scope);
            Info(transactionType.ToString(), transactionName, $"Trace '{transactionName}' started.", null);
            return transaction;
        }

        public ILoggingTransaction BeginSpan(string spanName, TxType spanType)
        {
            if (ScopeStack.Count == 0)
            {
                return BeginTrace(spanName, spanType);
            }
            var parentScope = ScopeStack.Peek();
            var newSpanId = Guid.NewGuid().ToString("N");
            var scope = new VbScope(parentScope.TraceId, newSpanId);
            var transaction = new LoggingTransaction(() => PopScope(scope));
            scope.Owner = transaction;
            ScopeStack.Push(scope);
            Info(spanType.ToString(), spanName, $"Span '{spanName}' started.", null);
            return transaction;
        }

        #endregion

        #region Standard Logging Methods
        public void Trace(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Trace", codeFile, methodName, message, null, null, null, null, properties);
        public void Debug(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Debug", codeFile, methodName, message, null, null, null, null, properties);
        public void Info(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Info", codeFile, methodName, message, null, null, null, null, properties);
        public void Warn(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Warn", codeFile, methodName, message, null, null, null, null, properties);
        public void Error(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Error", codeFile, methodName, message, message, null, null, null, properties);
        public void Fatal(string codeFile, string methodName, string message, [Optional] object properties)
            => Log("Fatal", codeFile, methodName, message, message, null, null, null, properties);
        public void ErrorHandler(string codeFile, string methodName, string message, string errorDescription, long errorNumber, string errorSource, int lineNumber, [Optional] object properties)
            => Log("Error", codeFile, methodName, message, errorDescription, errorNumber, errorSource, lineNumber, properties);
        #endregion

        #region Private Implementation

        private static void PopScope(VbScope scope)
        {
            if (ScopeStack.Count == 0) return;
            if (ScopeStack.Peek().Owner != scope.Owner)
            {
                LogManager.InternalLogger.Warn($"Out-of-order disposal detected. A child span was likely not disposed before its parent. TraceId: {scope.TraceId}");
                return;
            }
            ScopeStack.Pop();
        }

        private void Log(string level, string codeFile, string methodName, string message, string errorDescription, long? errorNumber, string errorSource, int? lineNumber, object comProperties)
        {
            var logger = LogManager.GetLogger($"{LogManager.GetAbstractedContextProperty("service.name")}.{codeFile}");
            var finalProps = BuildProperties(codeFile, methodName, comProperties);

            if (ScopeStack.Count > 0)
            {
                var currentScope = ScopeStack.Peek();
                finalProps["trace.id"] = currentScope.TraceId;
                var rootScope = ScopeStack.ToArray()[ScopeStack.Count - 1]; // More robust way to get bottom of stack
                finalProps["transaction.id"] = rootScope.SpanId;
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

        private Dictionary<string, object> ConvertComObjectToDictionary(object comObject)
        {
            if (comObject == null) return new Dictionary<string, object>();

            if (comObject is Dictionary<string, object> netDict)
            {
                return new Dictionary<string, object>(netDict);
            }

            if (comObject is IDictionary scriptingDict)
            {
                var dict = new Dictionary<string, object>();
                var keys = scriptingDict.Keys() as object[];
                if (keys != null)
                {
                    // THE FIX: Use a traditional 'for' loop to avoid issues with passing
                    // a 'foreach' variable by reference.
                    for (int i = 0; i < keys.Length; i++)
                    {
                        object key = keys[i];
                        // THE FIX: Call the explicit 'get_Item' accessor method, which is what the
                        // compiler error message (CS1545) recommended.
                        dict[key.ToString()] = SanitizeValue(scriptingDict.get_Item(ref key));
                    }
                }
                return dict;
            }

            var fallbackDict = new Dictionary<string, object>();
            try
            {
                dynamic dynamicComObject = comObject;
                foreach (var key in dynamicComObject.Keys())
                {
                    fallbackDict[key.ToString()] = SanitizeValue(dynamicComObject.Item(key));
                }
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Warn("Failed to convert COM properties object.", ex);
            }
            return fallbackDict;
        }

        private object SanitizeValue(object value)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsPrimitive || value is string || value is decimal || value is DateTime) return value;
            try
            {
                try { return ((dynamic)value).ToLogString(); }
                catch (RuntimeBinderException) { return value.ToString(); }
            }
            catch (Exception) { return "[Unsupported COM Object]"; }
        }
        #endregion
    }
}