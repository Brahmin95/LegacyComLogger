using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

// This attribute makes the internal members of this assembly visible to the test project,
// allowing tests to set internal properties like LogManager.Tracer for mocking purposes.
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
        private static ILoggerFactory _factory;
        private static readonly object _initializationLock = new object();
        private static readonly ConcurrentDictionary<string, object> _contextCache = new ConcurrentDictionary<string, object>();

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
        /// into the underlying logging provider (e.g., to NLog's MappedDiagnosticsLogicalContext).
        /// This is set by the provider's ILoggerFactory during initialization.
        /// </summary>
        public static Action<string, object> SetContextProperty { get; set; } = (key, value) => { };

        /// <summary>
        /// Initializes the entire logging system by loading the specified provider assembly via reflection.
        /// This is the primary entry point for any application using the framework.
        /// The call is idempotent; subsequent calls after a successful initialization will do nothing.
        /// </summary>
        /// <param name="providerAssemblyName">The simple name of the logging provider assembly (e.g., "MyCompany.Logging.NLogProvider").</param>
        /// <param name="environment">The type of application environment (DotNet or Vb6).</param>
        public static void Initialize(string providerAssemblyName, ApplicationEnvironment environment)
        {
            // The main try-catch is now at the top level to handle ANY failure during initialization,
            // including Assembly.Load or Type.GetType, which occur before the InternalLogger is created.
            try
            {
                if (IsInitialized) return;
                lock (_initializationLock)
                {
                    if (IsInitialized) return;
                    PerformInitialization(providerAssemblyName, environment);
                }
            }
            catch (Exception ex)
            {
                // This is the absolute "last gasp" for error reporting. It is used if any part
                // of the initialization fails, even before the real InternalLogger is available.
                string fatalErrorMsg = "CRITICAL: The application's core logging system could not be initialized. " +
                                       "This is a severe configuration or deployment issue (e.g., missing DLLs or incorrect type names).\n\n" +
                                       "Exception: " + ex.Message;

                Trace.WriteLine($"FATAL BOOTSTRAP ERROR: {fatalErrorMsg}\n\nFull Exception: {ex}");
                WriteToEventLog(fatalErrorMsg);

                string userPrompt = fatalErrorMsg + "\n\nIt is strongly recommended that you DO NOT PROCEED and contact IT Support.\n\n" +
                                    "Do you wish to proceed anyway (NOT RECOMMENDED)?";

                if (!AskUserToOverrideSafetyRecommendation(userPrompt))
                {
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// The core logic for loading the provider assembly and instantiating its components.
        /// This is wrapped by the public Initialize method's exception handling.
        /// </summary>
        private static void PerformInitialization(string providerAssemblyName, ApplicationEnvironment environment)
        {
            // These type names are part of the contract with any logging provider.
            const string factoryTypeName = "NLogLoggerFactory";
            const string internalLoggerTypeName = "NLogInternalLogger";
            const string initializerTypeName = "NLogInitializer";
            const string tracerTypeName = "ElasticApmTracer";

            string factoryFullName = $"{providerAssemblyName}.{factoryTypeName}";
            string internalLoggerFullName = $"{providerAssemblyName}.{internalLoggerTypeName}";
            string initializerFullName = $"{providerAssemblyName}.{initializerTypeName}";
            string tracerFullName = $"{providerAssemblyName}.{tracerTypeName}";

            // The core of the decoupled design: load the provider at runtime. Any failure here
            // will be caught by the top-level handler in the public Initialize method.
            var assembly = Assembly.Load(providerAssemblyName);
            var internalLoggerType = assembly.GetType(internalLoggerFullName, throwOnError: true);
            var factoryType = assembly.GetType(factoryFullName, throwOnError: true);
            var initializerType = assembly.GetType(initializerFullName, throwOnError: true);
            var tracerType = assembly.GetType(tracerFullName, throwOnError: true);

            // --- Bootstrap Phase: Internal Logger ---
            // If this fails, the top-level handler will catch it.
            InternalLogger = (IInternalLogger)Activator.CreateInstance(internalLoggerType);
            InternalLogger.Info("Internal logger bootstrapped successfully.");

            // --- Main Initialization Phase ---
            // From this point on, we can log failures to the newly created InternalLogger.
            try
            {
                // Create the factory that will produce ILogger instances.
                _factory = (ILoggerFactory)Activator.CreateInstance(factoryType);

                // Create the tracer that will handle APM transactions.
                Tracer = (ITracer)Activator.CreateInstance(tracerType);

                // Dynamically invoke the correct static configuration method based on the environment.
                string methodName = environment == ApplicationEnvironment.Vb6 ? "ConfigureVb6Context" : "ConfigureDotNetContext";
                var configureMethod = initializerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                configureMethod?.Invoke(null, null);

                InternalLogger.Info($"Main logging factory and tracer initialized successfully for '{environment}' environment.");
            }
            catch (Exception ex)
            {
                // If the main factory fails but bootstrap succeeded, we log to the internal logger.
                InternalLogger.Fatal("FATAL BOOTSTRAP ERROR: Could not create or configure the main LoggerFactory or Tracer.", ex);
                // We re-throw so the top-level handler displays the error to the user.
                throw;
            }
        }

        /// <summary>
        /// Sets a global context property that can be used by the logging provider.
        /// This caches the value locally and pushes it to the provider via the SetContextProperty delegate.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        public static void SetAbstractedContextProperty(string key, object value)
        {
            _contextCache[key] = value;
            SetContextProperty?.Invoke(key, value);
        }

        /// <summary>
        /// Gets a global context property from the local cache.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <returns>The cached value, or null if not found.</returns>
        public static object GetAbstractedContextProperty(string key)
        {
            _contextCache.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// Gets a logger instance with the specified name. If the framework is not initialized,
        /// it returns a resilient NullLogger that safely does nothing.
        /// </summary>
        /// <param name="name">The name of the logger (e.g., a class name or module name).</param>
        /// <returns>An ILogger implementation.</returns>
        public static ILogger GetLogger(string name) => _factory?.GetLogger(name) ?? new NullLogger();

        /// <summary>
        /// Gets a logger for the current class, automatically using the class's full name as the logger name.
        /// </summary>
        /// <returns>An ILogger instance named after the calling class.</returns>
        public static ILogger GetCurrentClassLogger()
        {
            // A helper to simplify getting a logger in .NET code.
            // StackFrame(1) gets the info of the method that called this one.
            string className = new StackFrame(1, false).GetMethod().DeclaringType.FullName;
            return GetLogger(className);
        }

        /// <summary>
        /// Writes a message to the Windows Event Log as a fallback mechanism.
        /// </summary>
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
                // This can fail due to permissions. Our last resort is Trace.
                Trace.WriteLine($"FATAL: Failed to write to Windows Event Log. Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a non-blocking dialog to the user during critical initialization failure.
        /// </summary>
        private static bool AskUserToOverrideSafetyRecommendation(string message)
        {
            try
            {
                // Requires a reference to System.Windows.Forms.
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
                // This can fail in non-interactive sessions. Default to the safe option.
                Trace.WriteLine($"FATAL: Failed to show choice dialog. Defaulting to NOT proceed. Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// A logger implementation that performs no operations. This is returned by the LogManager
        /// before initialization is complete to prevent NullReferenceExceptions in consumer code.
        /// </summary>
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

        /// <summary>
        /// An internal logger that performs no operations, used before the bootstrap is complete.
        /// </summary>
        private class NullInternalLogger : IInternalLogger
        {
            public void Trace(string message, Exception exception = null) { }
            public void Debug(string message, Exception exception = null) { }
            public void Info(string message, Exception exception = null) { }
            public void Warn(string message, Exception exception = null) { }
            public void Error(string message, Exception exception = null) { }
            public void Fatal(string message, Exception exception = null) { }
        }

        /// <summary>
        /// An ITracer implementation that performs no operations. This is returned by the LogManager
        /// before initialization is complete to prevent NullReferenceExceptions in consumer code.
        /// </summary>
        private class NullTracer : ITracer
        {
            public void Trace(string name, TxType type, Action action) => action?.Invoke();
            public T Trace<T>(string name, TxType type, Func<T> func) => func != null ? func.Invoke() : default;
        }
    }
}