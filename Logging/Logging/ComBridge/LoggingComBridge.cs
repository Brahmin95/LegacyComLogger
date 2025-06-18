using Microsoft.CSharp.RuntimeBinder;
using MyCompany.Logging.Abstractions; // The ONLY framework 'using' statement needed at the top level.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MyCompany.Logging.ComBridge
{
    [ComVisible(true)]
    [Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("MyCompany.Logging.ComBridge")]
    public class LoggingComBridge : ILoggingComBridge
    {
        private readonly Abstractions.ILogger _logger;

        public LoggingComBridge()
        {
            // This is the correct, resilient initialization logic.
            EnsureLogManagerInitialized();

            // Now it is safe to get our logger instance.
            _logger = LogManager.GetLogger("Vb6ComBridge");
        }

        private void EnsureLogManagerInitialized()
        {
            // 1. Check the public property on the abstract LogManager.
            if (LogManager.IsInitialized) return;

            try
            {
                // These are the "magic strings" that define the runtime contract.
                const string providerAssembly = "MyCompany.Logging.NLogProvider";
                const string factoryTypeFullName = "MyCompany.Logging.NLogProvider.NLogLoggerFactory";

                // 2. Load the provider assembly from the application's directory at runtime.
                var assembly = Assembly.Load(providerAssembly);

                // 3. Get the factory type from the loaded assembly.
                var type = assembly.GetType(factoryTypeFullName);
                if (type == null) throw new TypeLoadException($"Cannot find type '{factoryTypeFullName}' in assembly '{providerAssembly}'.");

                // 4. Create an instance of the factory. Crucially, we pass "VB6" to its constructor.
                //    This tells the factory to run the VB6-specific context initialization.
                var factory = (ILoggerFactory)Activator.CreateInstance(type, "VB6");

                // 5. Initialize the static LogManager with our newly created factory.
                LogManager.Initialize(factory);
            }
            catch (Exception ex)
            {
                // If anything fails (DLL not found, type renamed, etc.), the app MUST NOT CRASH.
                // Logging will simply fall back to the NullLogger.
                // We can write to the system's debug trace as a last resort.
                System.Diagnostics.Trace.WriteLine($"CRITICAL: Logging framework initialization failed via reflection. {ex.Message}");
            }
        }

        // All other helper methods and logging method implementations are now correct
        // because they use the `_logger` field, which is guaranteed to be initialized.
        public string CreateTransactionId() => Guid.NewGuid().ToString("N");

        public object CreateProperties()
        {
            try { return Activator.CreateInstance(Type.GetTypeFromProgID("Scripting.Dictionary")); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"ERROR: Failed to create Scripting.Dictionary. {ex.Message}");
                return null;
            }
        }

        public object CreatePropertiesWithTransactionId()
        {
            dynamic props = CreateProperties();
            if (props != null) { props.Add("transactionId", CreateTransactionId()); }
            return props;
        }

        public void Trace(string cf, string mn, string m, object p) => Log("Trace", cf, mn, m, null, p);
        public void Debug(string cf, string mn, string m, object p) => Log("Debug", cf, mn, m, null, p);
        public void Info(string cf, string mn, string m, object p) => Log("Info", cf, mn, m, null, p);
        public void Warn(string cf, string mn, string m, object p) => Log("Warn", cf, mn, m, null, p);
        public void Error(string cf, string mn, string m, string ed, object p) => Log("Error", cf, mn, m, ed, p);
        public void Fatal(string cf, string mn, string m, string ed, object p) => Log("Fatal", cf, mn, m, ed, p);

        private void Log(string level, string codeFile, string methodName, string message, string errorDetails, object properties)
        {
            var finalProps = BuildProperties(codeFile, methodName, properties);
            if (errorDetails != null && errorDetails != Type.Missing.ToString() && !string.IsNullOrEmpty(errorDetails))
            {
                finalProps["vbErrorDetails"] = errorDetails;
            }
            switch (level)
            {
                case "Trace": _logger.Trace(message, finalProps); break;
                case "Debug": _logger.Debug(message, finalProps); break;
                case "Info": _logger.Info(message, finalProps); break;
                case "Warn": _logger.Warn(message, finalProps); break;
                case "Error": _logger.Error(message, ex: null, properties: finalProps); break;
                case "Fatal": _logger.Fatal(message, ex: null, properties: finalProps); break;
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
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"WARN: Failed to convert COM properties object. {ex.Message}"); }
            return dict;
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
    }
}