using Microsoft.CSharp.RuntimeBinder;
using MyCompany.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MyCompany.Logging.ComBridge
{
    /// <summary>
    /// The concrete implementation of the COM-visible logging bridge. This class acts as an
    /// adapter between the COM world (VB6) and the .NET logging abstraction (ILogger).
    /// </summary>
    [ComVisible(true)]
    [Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("MyCompany.Logging.ComBridge.LoggingComBridge")]
    public class LoggingComBridge : ILoggingComBridge
    {
        #region Constructor and Helpers
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

        /// <inheritdoc/>
        public string CreateTransactionId() => Guid.NewGuid().ToString("N");

        /// <inheritdoc/>
        public object CreateProperties()
        {
            try
            {
                // This uses late binding to create the COM object, which is required
                // for interop with VBScript/VBA components.
                return Activator.CreateInstance(Type.GetTypeFromProgID("Scripting.Dictionary"));
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Error("Failed to create Scripting.Dictionary. The 'Microsoft Scripting Runtime' may not be registered.", ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public object CreatePropertiesWithTransactionId()
        {
            dynamic props = CreateProperties();
            if (props != null) { props.Add("transaction.id", CreateTransactionId()); }
            return props;
        }
        #endregion

        #region Public Log Methods
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
        public void ErrorHandler(string codeFile, string methodName, string errorDescription, long errorNumber, string errorSource, int lineNumber, [Optional] string message, [Optional] object properties)
        {
            // If the optional message is not provided, we default to using the error description
            // as the main log message for consistency.
            string finalMessage = (message != null && message != Type.Missing.ToString() && !string.IsNullOrEmpty(message)) ? message : errorDescription;
            Log("Error", codeFile, methodName, finalMessage, errorDescription, errorNumber, errorSource, lineNumber, properties);
        }
        #endregion

        /// <summary>
        /// The private core logging method that all public log methods call. It handles both simple
        /// and structured error scenarios and creates the appropriate Exception object.
        /// </summary>
        private void Log(string level, string codeFile, string methodName, string message, string errorDescription, long? errorNumber, string errorSource, int? lineNumber, object properties)
        {
            string appName = LogManager.GetAbstractedContextProperty("service.name") as string ?? "Vb6App";
            string loggerName = $"{appName}.{codeFile}";
            var logger = LogManager.GetLogger(loggerName);

            var finalProps = BuildProperties(codeFile, methodName, properties);

            Exception exceptionForLogging = null;
            // Only create an exception object for Error and Fatal levels.
            if ((level == "Error" || level == "Fatal") && !string.IsNullOrEmpty(errorDescription))
            {
                if (errorNumber.HasValue)
                {
                    // Case 1: Called from ErrorHandler. Create the rich, structured exception object.
                    // The .NET code intelligently handles lineNumber if it's 0.
                    exceptionForLogging = new VBErrorException(errorDescription, errorNumber.Value, errorSource, lineNumber == 0 ? (int?)null : lineNumber);
                }
                else
                {
                    // Case 2: Called from simple Error/Fatal. Wrap the message in the custom exception
                    // to ensure a consistent 'error.type' in the final log.
                    exceptionForLogging = new VBErrorException(errorDescription);
                }
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

        #region Private Helpers
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
                foreach (var prop in vbProps) { dict[prop.Key] = prop.Value; }
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
        /// </summary>
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