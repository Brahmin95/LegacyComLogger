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
    [ClassInterface(ClassInterfaceType.None)] // We explicitly implement the interface for clean COM.
    [ProgId("MyCompany.Logging.ComBridge.LoggingComBridge")]
    public class LoggingComBridge : ILoggingComBridge
    {
        /// <summary>
        /// Initializes a new instance of the LoggingComBridge class.
        /// The constructor ensures that the central LogManager is initialized if it hasn't been already.
        /// </summary>
        public LoggingComBridge()
        {
            // This is the single, consistent entry point for initialization from a COM client.
            // If the logger is already initialized (e.g., by another component in the same process),
            // this call will safely do nothing.
            if (!LogManager.IsInitialized)
            {
                LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.Vb6);
            }
        }

        /// <summary>
        /// Creates a new, unique transaction ID string (a GUID without dashes).
        /// </summary>
        /// <returns>A new transaction ID string.</returns>
        public string CreateTransactionId() => Guid.NewGuid().ToString("N");

        /// <summary>
        /// Creates a new Scripting.Dictionary object for holding custom log properties.
        /// This is a helper method for VB6 developers.
        /// </summary>
        /// <returns>A COM object that can be used as a dictionary.</returns>
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

        /// <summary>
        /// Creates a new Scripting.Dictionary and pre-populates it with a new transaction ID.
        /// </summary>
        /// <returns>A COM dictionary containing a 'transaction.id' key.</returns>
        public object CreatePropertiesWithTransactionId()
        {
            dynamic props = CreateProperties();
            if (props != null) { props.Add("transaction.id", CreateTransactionId()); }
            return props;
        }

        // --- ILoggingComBridge Method Implementations ---
        // These methods simply delegate to the private Log method with the correct log level.
        #region Public Log Methods

        /// <inheritdoc/>
        public void Trace(string codeFile, string methodName, string message, [Optional] object properties) => Log("Trace", codeFile, methodName, message, null, properties);
        /// <inheritdoc/>
        public void Debug(string codeFile, string methodName, string message, [Optional] object properties) => Log("Debug", codeFile, methodName, message, null, properties);
        /// <inheritdoc/>
        public void Info(string codeFile, string methodName, string message, [Optional] object properties) => Log("Info", codeFile, methodName, message, null, properties);
        /// <inheritdoc/>
        public void Warn(string codeFile, string methodName, string message, [Optional] object properties) => Log("Warn", codeFile, methodName, message, null, properties);
        /// <inheritdoc/>
        public void Error(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties) => Log("Error", codeFile, methodName, message, errorDetails, properties);
        /// <inheritdoc/>
        public void Fatal(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties) => Log("Fatal", codeFile, methodName, message, errorDetails, properties);

        #endregion


        /// <summary>
        /// The private core logging method that all public log methods call.
        /// It constructs the logger name, builds the properties dictionary, and calls the abstract ILogger.
        /// </summary>
        private void Log(string level, string codeFile, string methodName, string message, string errorDetails, object properties)
        {
            // Dynamically construct a logger name from the application and file name.
            // This allows for granular filtering in NLog.config (e.g., name="LegacyApp.exe.frmOrders.frm.*").
            string appName = LogManager.GetAbstractedContextProperty("service.name") as string ?? "Vb6App";
            string loggerName = $"{appName}.{codeFile}";
            var logger = LogManager.GetLogger(loggerName);

            // Build the final dictionary of properties for the log event.
            var finalProps = BuildProperties(codeFile, methodName, properties);

            Exception exceptionForLogging = null;
            if (errorDetails != null && errorDetails != Type.Missing.ToString() && !string.IsNullOrEmpty(errorDetails))
            {
                // To get a proper stack trace in logging systems like Elastic, it's best to
                // wrap the error string in a real Exception object.
                finalProps["vbErrorDetails"] = errorDetails;
                exceptionForLogging = new Exception(errorDetails);
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
            // These special properties are added so the NLogProvider can later translate them
            // into NLog-specific call site properties for the ecs-layout.
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
                // Using 'dynamic' allows us to call methods like .Keys() and .Item() on the COM
                // object without needing a hard reference to the Scripting Runtime library.
                dynamic scriptDict = comObject;
                foreach (var key in scriptDict.Keys())
                {
                    dict[key.ToString()] = SanitizeValue(scriptDict.Item(key));
                }
            }
            catch (Exception ex)
            {
                // This will catch errors if the passed object isn't actually a dictionary.
                LogManager.InternalLogger.Warn("Failed to convert COM properties object. It may not be a Scripting.Dictionary.", ex);
            }
            return dict;
        }

        /// <summary>
        /// Sanitizes a value from a COM object before adding it to the log properties.
        /// It attempts to convert complex COM objects to a string using a "ToLogString()" convention.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>A log-safe representation of the value.</returns>
        private object SanitizeValue(object value)
        {
            if (value == null) return null;
            var type = value.GetType();

            // Primitive types are safe to log directly.
            if (type.IsPrimitive || value is string || value is decimal || value is DateTime) return value;

            // For complex COM objects, we establish a convention.
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
    }
}