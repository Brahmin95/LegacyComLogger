using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

// This attribute makes the internal members of this assembly visible to the test project,
// allowing tests to modify internal state for testing purposes.
[assembly: InternalsVisibleTo("MyCompany.Logging.Tests")]

namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// A contract for a factory that can create instances of ILogger.
    /// This is the primary interface that a logging provider must implement.
    /// </summary>
    public interface ILoggerFactory
    {
        /// <summary>
        /// Gets a logger instance with the specified name.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>An ILogger implementation.</returns>
        ILogger GetLogger(string name);
    }

    /// <summary>
    /// Provides the central static entry point for the logging framework. It is responsible for
    /// discovering and initializing the logging provider at runtime and acts as the service
    /// locator for ILogger instances. This class is thread-safe.
    /// </summary>
    public static class LogManager
    {
        // Provider details are now internal static fields that hold the full type names,
        // which is a more robust design.
        internal static string ProviderAssemblyName = "MyCompany.Logging.NLogProvider";
        internal static string FactoryFullTypeName = "MyCompany.Logging.NLogProvider.NLogLoggerFactory";
        internal static string InternalLoggerFullTypeName = "MyCompany.Logging.NLogProvider.NLogInternalLogger";
        internal static string InitializerFullTypeName = "MyCompany.Logging.NLogProvider.NLogInitializer";
        internal static string TracerFullTypeName = "MyCompany.Logging.NLogProvider.ElasticApmTracer";

        private static ILoggerFactory _factory;
        private static readonly object _initializationLock = new object();
        private static readonly ConcurrentDictionary<string, object> _contextCache = new ConcurrentDictionary<string, object>();
        private static Assembly _providerAssembly;

        /// <summary>
        /// Initializes static members of the LogManager class.
        /// </summary>
        static LogManager()
        {
            SafetyOverridePrompt = ShowWindowsFormsDialog;
        }

        /// <summary>
        /// Gets or sets the delegate used to prompt the user during a critical, unrecoverable
        /// initialization failure. This is internal and exposed to the test assembly.
        /// The function should return `true` to proceed, or `false` to exit the application.
        /// </summary>
        internal static Func<string, bool> SafetyOverridePrompt { get; set; }

        /// <summary>
        /// Gets a value indicating whether the main logger factory has been successfully initialized.
        /// </summary>
        public static bool IsInitialized => _factory != null;

        /// <summary>
        /// Gets the internal logger for logging framework-specific messages. Before initialization,
        /// this returns a NullInternalLogger that does nothing. After bootstrap, it returns the
        /// provider's actual internal logger implementation.
        /// </summary>
        public static IInternalLogger InternalLogger { get; private set; } = new NullInternalLogger();

        /// <summary>
        /// Gets the provider-agnostic tracer for creating APM transactions. Before initialization,
        /// this returns a NullTracer that does nothing but execute the wrapped code. After
        /// initialization, it returns the provider's actual tracer implementation.
        /// The setter is internal but visible to the test assembly via `InternalsVisibleTo`.
        /// </summary>
        public static ITracer Tracer { get; internal set; } = new NullTracer();

        /// <summary>
        /// Gets or sets a delegate that allows the LogManager to push context properties
        /// into the underlying logging provider.
        /// </summary>
        public static Action<string, object> SetContextProperty { get; set; } = (key, value) => { };

        /// <summary>
        /// Initializes the logging system. This method handles the one-time bootstrap
        /// and configures the context for the specified runtime environment.
        /// It is safe to call multiple times; the expensive bootstrap runs only once,
        /// but the context will be updated on each call to ensure it is correct.
        /// </summary>
        /// <param name="environment">The type of application runtime (DotNet or Vb6).</param>
        public static void Initialize(AppRuntime environment)
        {
            try
            {
                // The expensive, one-time-only bootstrap is protected by a lock.
                if (!IsInitialized)
                {
                    lock (_initializationLock)
                    {
                        if (!IsInitialized)
                        {
                            BootstrapProvider();
                        }
                    }
                }

                // The lightweight context configuration now runs every time Initialize is called.
                // This allows the ComBridge to "correct" the context from .NET to VB6 if needed.
                ConfigureContext(environment);
            }
            catch (Exception ex)
            {
                string fatalErrorMsg = "CRITICAL: The application's core logging system could not be initialized. " +
                                       "This is a severe configuration or deployment issue (e.g., missing DLLs or incorrect type names).\n\n" +
                                       "Exception: " + ex.Message;

                Trace.WriteLine($"FATAL BOOTSTRAP ERROR: {fatalErrorMsg}\n\nFull Exception: {ex}");
                WriteToEventLog(fatalErrorMsg);

                string userPrompt = fatalErrorMsg + "\n\nIt is strongly recommended that you DO NOT PROCEED and contact IT Support.\n\n" +
                                    "Do you wish to proceed anyway (NOT RECOMMENDED)?";

                if (!SafetyOverridePrompt(userPrompt))
                {
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// Performs the expensive, one-time-only loading of the provider and instantiation of components.
        /// </summary>
        private static void BootstrapProvider()
        {
            _providerAssembly = Assembly.Load(ProviderAssemblyName);
            var internalLoggerType = _providerAssembly.GetType(InternalLoggerFullTypeName, throwOnError: true);
            var factoryType = _providerAssembly.GetType(FactoryFullTypeName, throwOnError: true);
            var tracerType = _providerAssembly.GetType(TracerFullTypeName, throwOnError: true);

            InternalLogger = (IInternalLogger)Activator.CreateInstance(internalLoggerType);
            InternalLogger.Info("Internal logger bootstrapped successfully.");

            try
            {
                _factory = (ILoggerFactory)Activator.CreateInstance(factoryType);
                Tracer = (ITracer)Activator.CreateInstance(tracerType);
            }
            catch (Exception ex)
            {
                InternalLogger.Fatal("FATAL BOOTSTRAP ERROR: Could not create the main LoggerFactory or Tracer.", ex);
                throw;
            }
        }

        /// <summary>
        /// Configures the logging context based on the specified runtime environment.
        /// This method is lightweight and safe to call multiple times.
        /// </summary>
        private static void ConfigureContext(AppRuntime environment)
        {
            if (!IsInitialized) return; // Cannot configure context before bootstrap

            var initializerType = _providerAssembly.GetType(InitializerFullTypeName, throwOnError: true);
            string methodName = environment == AppRuntime.Vb6 ? "ConfigureVb6Context" : "ConfigureDotNetContext";
            var configureMethod = initializerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            configureMethod?.Invoke(null, null);

            InternalLogger.Info($"Logging context configured for '{environment}' environment.");
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
                const string eventSource = "MyLegacyApplication";
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

        private static bool ShowWindowsFormsDialog(string message)
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

        private class NullLogger : ILogger
        {
            public void Trace(string mt, params object[] a) { }
            public void Debug(string mt, params object[] a) { }
            public void Info(string mt, params object[] a) { }
            public void Warn(string mt, params object[] a) { }
            public void Error(Exception ex, string mt, params object[] a) { }
            public void Fatal(Exception ex, string mt, params object[] a) { }
            public void Trace(string m, System.Collections.Generic.Dictionary<string, object> p = null) { }
            public void Debug(string m, System.Collections.Generic.Dictionary<string, object> p = null) { }
            public void Info(string m, System.Collections.Generic.Dictionary<string, object> p = null) { }
            public void Warn(string m, System.Collections.Generic.Dictionary<string, object> p = null) { }
            public void Error(string m, Exception ex = null, System.Collections.Generic.Dictionary<string, object> p = null) { }
            public void Fatal(string m, Exception ex = null, System.Collections.Generic.Dictionary<string, object> p = null) { }
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

        private class NullTracer : ITracer
        {
            public void Trace(string name, TxType type, Action action) => action?.Invoke();
            public T Trace<T>(string name, TxType type, Func<T> func) => func != null ? func.Invoke() : default;
        }
    }
}