using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using System;
using System.Reflection;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// An abstract base class for logging tests that provides setup and teardown logic
    /// to reset the static state of the LogManager and other related components.
    /// This is crucial for ensuring that tests run in isolation and do not interfere with each other.
    /// </summary>
    [Collection("SequentialLoggingTests")] // Xunit collection fixture to force tests in this collection to run sequentially, not in parallel.
    public abstract class LoggingTestBase : IDisposable
    {
        /// <summary>
        /// The environment variable name used for session correlation ID tests.
        /// </summary>
        protected const string TestCorrelationIdEnvVar = "MYAPP_SESSION_ID";

        /// <summary>
        /// Initializes a new instance of the LoggingTestBase class.
        /// Resets all static state before each test runs.
        /// </summary>
        protected LoggingTestBase()
        {
            ResetAllStaticState();
        }

        /// <summary>
        /// Disposes of the test class resources.
        /// Resets all static state after each test runs to ensure a clean slate for the next test.
        /// </summary>
        public void Dispose()
        {
            ResetAllStaticState();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Uses reflection to reset all static fields and properties in the logging framework
        /// to their default, uninitialized state. This is the core of test isolation.
        /// </summary>
        protected void ResetAllStaticState()
        {
            var logManagerType = typeof(LogManager);

            // Reset the static _factory field in LogManager to null.
            var factoryField = logManagerType.GetField("_factory", BindingFlags.NonPublic | BindingFlags.Static);
            factoryField?.SetValue(null, null);

            // Reset the static InternalLogger property to a new instance of the private NullInternalLogger.
            var internalLoggerProperty = logManagerType.GetProperty("InternalLogger", BindingFlags.Public | BindingFlags.Static);
            var nullInternalLoggerType = logManagerType.GetNestedType("NullInternalLogger", BindingFlags.NonPublic);
            var nullInternalLoggerInstance = Activator.CreateInstance(nullInternalLoggerType);
            internalLoggerProperty?.SetValue(null, nullInternalLoggerInstance, null);

            // Reset the static Tracer property to a new instance of the private NullTracer.
            var tracerProperty = logManagerType.GetProperty("Tracer", BindingFlags.Public | BindingFlags.Static);
            var nullTracerType = logManagerType.GetNestedType("NullTracer", BindingFlags.NonPublic);
            var nullTracerInstance = Activator.CreateInstance(nullTracerType);
            tracerProperty?.SetValue(null, nullTracerInstance, null);

            // Reset the SafetyOverridePrompt delegate back to its default production implementation.
            var promptDelegateProperty = logManagerType.GetProperty("SafetyOverridePrompt", BindingFlags.NonPublic | BindingFlags.Static);
            var defaultPromptMethod = logManagerType.GetMethod("ShowWindowsFormsDialog", BindingFlags.NonPublic | BindingFlags.Static);
            var defaultDelegate = Delegate.CreateDelegate(typeof(Func<string, bool>), defaultPromptMethod);
            promptDelegateProperty?.SetValue(null, defaultDelegate);

            // Reset the internal static provider configuration fields to their production defaults.
            logManagerType.GetField("ProviderAssemblyName", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, "MyCompany.Logging.NLogProvider");
            logManagerType.GetField("FactoryFullTypeName", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, "MyCompany.Logging.NLogProvider.NLogLoggerFactory");
            logManagerType.GetField("InternalLoggerFullTypeName", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, "MyCompany.Logging.NLogProvider.NLogInternalLogger");
            logManagerType.GetField("InitializerFullTypeName", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, "MyCompany.Logging.NLogProvider.NLogInitializer");
            logManagerType.GetField("TracerFullTypeName", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, "MyCompany.Logging.NLogProvider.ElasticApmTracer");

            // Reset our NLogInitializer's static _isInitialized flag to false.
            // Check for null in case the NLogProvider assembly isn't referenced by some test projects.
            var nlogInitializerType = Type.GetType("MyCompany.Logging.NLogProvider.NLogInitializer, MyCompany.Logging.NLogProvider");
            if (nlogInitializerType != null)
            {
                var initializerIsInitializedField = nlogInitializerType.GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Static);
                initializerIsInitializedField?.SetValue(null, false);
            }

            // Reset NLog's own static state.
            NLog.LogManager.Configuration = null;
            NLog.GlobalDiagnosticsContext.Clear();

            // Clean up any environment variables set by tests.
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, null);
        }

        /// <summary>
        /// A test-specific helper method to inject mock implementations into the static LogManager
        /// for unit testing purposes, bypassing the full reflection-based initialization.
        /// </summary>
        protected void InitializeWithMocks(ILoggerFactory factory, IInternalLogger internalLogger)
        {
            var logManagerType = typeof(LogManager);

            var factoryField = logManagerType.GetField("_factory", BindingFlags.NonPublic | BindingFlags.Static);
            factoryField?.SetValue(null, factory);

            var internalLoggerProperty = logManagerType.GetProperty("InternalLogger", BindingFlags.Public | BindingFlags.Static);
            internalLoggerProperty?.SetValue(null, internalLogger, null);
        }
    }
}