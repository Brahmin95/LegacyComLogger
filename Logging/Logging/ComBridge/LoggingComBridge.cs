using Microsoft.CSharp.RuntimeBinder;
using MyCompany.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MyCompany.Logging.ComBridge
{
    [ComVisible(true)]
    [Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("MyCompany.Logging.ComBridge.LoggingComBridge")]
    public class LoggingComBridge : ILoggingComBridge
    {
        public LoggingComBridge()
        {
            // The constructor is now incredibly simple, clean, and consistent.
            if (!LogManager.IsInitialized)
            {
                // It uses the same pattern as the .NET startup, specifying the provider
                // assembly and the correct environment type.
                LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.Vb6);
            }
        }

        public string CreateTransactionId() => Guid.NewGuid().ToString("N");

        public object CreateProperties()
        {
            try { return Activator.CreateInstance(Type.GetTypeFromProgID("Scripting.Dictionary")); }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Error("Failed to create Scripting.Dictionary", ex);
                return null;
            }
        }

        public object CreatePropertiesWithTransactionId()
        {
            dynamic props = CreateProperties();
            if (props != null) { props.Add("transaction.id", CreateTransactionId()); }
            return props;
        }

        // --- ILoggingComBridge Method Implementations ---
        public void Trace(string codeFile, string methodName, string message, [Optional] object properties) => Log("Trace", codeFile, methodName, message, null, properties);
        public void Debug(string codeFile, string methodName, string message, [Optional] object properties) => Log("Debug", codeFile, methodName, message, null, properties);
        public void Info(string codeFile, string methodName, string message, [Optional] object properties) => Log("Info", codeFile, methodName, message, null, properties);
        public void Warn(string codeFile, string methodName, string message, [Optional] object properties) => Log("Warn", codeFile, methodName, message, null, properties);
        public void Error(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties) => Log("Error", codeFile, methodName, message, errorDetails, properties);
        public void Fatal(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties) => Log("Fatal", codeFile, methodName, message, errorDetails, properties);


        // --- Private Implementation Details ---

        private void Log(string level, string codeFile, string methodName, string message, string errorDetails, object properties)
        {
            string appName = LogManager.GetAbstractedContextProperty("service.name") as string ?? "Vb6App";
            string loggerName = $"{appName}.{codeFile}";
            var logger = LogManager.GetLogger(loggerName);
            var finalProps = BuildProperties(codeFile, methodName, properties);

            Exception exceptionForLogging = null;
            if (errorDetails != null && errorDetails != Type.Missing.ToString() && !string.IsNullOrEmpty(errorDetails))
            {
                finalProps["vbErrorDetails"] = errorDetails;
                exceptionForLogging = new Exception(errorDetails);
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
                foreach (var prop in vbProps) { dict[prop.Key] = prop.Value; }
            }
            return dict;
        }

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
                LogManager.InternalLogger.Warn("Failed to convert COM properties object.", ex);
            }
            return dict;
        }

        /// <summary>
        /// this makes a nest effort attempt to make a string from the COM object.
        /// The last attempt is to call a well known member called 'ToLogString'.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
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
    }
}