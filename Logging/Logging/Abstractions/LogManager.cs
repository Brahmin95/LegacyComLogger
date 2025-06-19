using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace MyCompany.Logging.Abstractions
{
    public interface ILoggerFactory
    {
        ILogger GetLogger(string name);
    }

    public static class LogManager
    {
        private static ILoggerFactory _factory;
        private static bool _isBootstrapInitialized = false;
        private static readonly object _initializationLock = new object();
        private static readonly ConcurrentDictionary<string, object> _contextCache = new ConcurrentDictionary<string, object>();

        public static bool IsInitialized => _factory != null;
        public static IInternalLogger InternalLogger { get; private set; } = new NullInternalLogger();
        public static Action<string, object> SetContextProperty { get; set; } = (key, value) => { };

        public static void Initialize(string providerAssemblyName, ApplicationEnvironment environment)
        {
            if (IsInitialized) return;
            lock (_initializationLock)
            {
                if (IsInitialized) return;
                try
                {
                    const string factoryTypeName = "NLogLoggerFactory";
                    const string internalLoggerTypeName = "NLogInternalLogger";
                    const string initializerTypeName = "NLogInitializer";

                    string factoryFullName = $"{providerAssemblyName}.{factoryTypeName}";
                    string internalLoggerFullName = $"{providerAssemblyName}.{internalLoggerTypeName}";
                    string initializerFullName = $"{providerAssemblyName}.{initializerTypeName}";

                    var assembly = Assembly.Load(providerAssemblyName);
                    var factoryType = assembly.GetType(factoryFullName, throwOnError: true);
                    var internalLoggerType = assembly.GetType(internalLoggerFullName, throwOnError: true);
                    var initializerType = assembly.GetType(initializerFullName, throwOnError: true);

                    InitializeBootstrap(internalLoggerType);
                    InitializeMainFactory(factoryType, initializerType, environment);
                }
                catch (Exception ex)
                {
                    InternalLogger.Fatal("Failed to initialize logging provider via reflection. A required DLL is likely missing or a type name has changed.", ex);
                }
            }
        }

        public static void InitializeBootstrap(Type internalLoggerType)
        {
            if (_isBootstrapInitialized) return;
            try
            {
                InternalLogger = (IInternalLogger)Activator.CreateInstance(internalLoggerType);
            }
            catch (Exception ex)
            {
                string fatalErrorMsg = "CRITICAL: The application's core logging system could not be initialized. " +
                                       "This is a severe configuration or deployment issue (e.g., missing DLLs).\n\n" +
                                       "It is strongly recommended that you DO NOT PROCEED and contact IT Support.\n\n" +
                                       "Do you wish to proceed anyway (NOT RECOMMENDED)?";

                Trace.WriteLine($"FATAL BOOTSTRAP ERROR: {fatalErrorMsg}\n\nException: {ex}");
                WriteToEventLog($"FATAL BOOTSTRAP ERROR: Could not create InternalLogger of type '{internalLoggerType?.Name ?? "Unknown"}'. Exception: {ex}");
                if (!AskUserToOverrideSafetyRecommendation(fatalErrorMsg))
                {
                    Environment.Exit(1);
                }
            }
            finally
            {
                _isBootstrapInitialized = true;
            }
        }

        public static void InitializeMainFactory(Type loggerFactoryType, Type nlogInitializerType, ApplicationEnvironment environment)
        {
            if (!_isBootstrapInitialized)
            {
                Trace.WriteLine("FATAL DEVELOPER ERROR: InitializeBootstrap() was not called before InitializeMainFactory().");
                return;
            }
            if (IsInitialized) return;
            try
            {
                _factory = (ILoggerFactory)Activator.CreateInstance(loggerFactoryType);

                string methodName = environment == ApplicationEnvironment.Vb6 ? "ConfigureVb6Context" : "ConfigureDotNetContext";
                var configureMethod = nlogInitializerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                configureMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                InternalLogger.Fatal("FATAL BOOTSTRAP ERROR: Could not create or configure the main LoggerFactory.", ex);
            }
        }

        public static void SetAbstractedContextProperty(string key, object value)
        {
            _contextCache[key] = value;
            SetContextProperty?.Invoke(key, value);
        }

        public static object GetAbstractedContextProperty(string key)
        {
            _contextCache.TryGetValue(key, out var value);
            return value;
        }

        public static ILogger GetLogger(string name) => _factory?.GetLogger(name) ?? new NullLogger();

        public static ILogger GetCurrentClassLogger()
        {
            string className = new StackFrame(1, false).GetMethod().DeclaringType.FullName;
            return GetLogger(className);
        }

        private static void WriteToEventLog(string message)
        {
            try
            {
                string eventSource = "MyLegacyApplication";
                if (!EventLog.SourceExists(eventSource))
                {
                    EventLog.CreateEventSource(eventSource, "Application");
                }
                EventLog.WriteEntry(eventSource, message, EventLogEntryType.Error);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"FATAL: Failed to write to Windows Event Log. Exception: {ex.Message}");
            }
        }

        private static bool AskUserToOverrideSafetyRecommendation(string message)
        {
            try
            {
                var result = System.Windows.Forms.MessageBox.Show(
                    message, "Critical Logging Failure - Proceed with caution",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Error,
                    System.Windows.Forms.MessageBoxDefaultButton.Button2
                );
                return result == System.Windows.Forms.DialogResult.Yes;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"FATAL: Failed to show choice dialog. Defaulting to NOT proceed. Exception: {ex.Message}");
                return false;
            }
        }

        // ===================================================================
        // FULL IMPLEMENTATION OF THE NULL OBJECT PATTERNS
        // ===================================================================

        private class NullLogger : ILogger
        {
            public void Trace(string messageTemplate, params object[] args) { }
            public void Debug(string messageTemplate, params object[] args) { }
            public void Info(string messageTemplate, params object[] args) { }
            public void Warn(string messageTemplate, params object[] args) { }
            public void Error(Exception ex, string messageTemplate, params object[] args) { }
            public void Fatal(Exception ex, string messageTemplate, params object[] args) { }
            public void Trace(string message, Dictionary<string, object> properties = null) { }
            public void Debug(string message, Dictionary<string, object> properties = null) { }
            public void Info(string message, Dictionary<string, object> properties = null) { }
            public void Warn(string message, Dictionary<string, object> properties = null) { }
            public void Error(string message, Exception ex = null, Dictionary<string, object> properties = null) { }
            public void Fatal(string message, Exception ex = null, Dictionary<string, object> properties = null) { }
        }

        private class NullInternalLogger : IInternalLogger
        {
            public void Trace(string message, Exception exception = null) { }
            public void Debug(string message, Exception exception = null) { }
            public void Info(string message, Exception exception = null) { }
            public void Warn(string message, Exception exception = null) { }
            public void Error(string message, Exception exception = null) { }
            public void Fatal(string message, Exception exception = null) { }
        }
    }
}