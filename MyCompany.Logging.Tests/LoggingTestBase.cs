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

            // Reset our NLogInitializer's static _isInitialized flag to false.
            var initializerIsInitializedField = typeof(NLogInitializer).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            if (initializerIsInitializedField == null)
            {
                throw new InvalidOperationException("Could not find the private static field '_isInitialized' in NLogInitializer.");
            }
            initializerIsInitializedField.SetValue(null, false);

            // Reset NLog's own static state.
            NLog.LogManager.Configuration = null;

            // Use the modern API to clear the global context for test isolation.
            NLog.GlobalDiagnosticsContext.Clear();

            // Clean up any environment variables set by tests.
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, null);
        }

        /// <summary>
        /// A test-specific helper method to inject mock implementations into the static LogManager
        /// for unit testing purposes, bypassing the full reflection-based initialization.
        /// </summary>
        /// <param name="factory">The mock ILoggerFactory to inject.</param>
        /// <param name="internalLogger">The mock IInternalLogger to inject.</param>
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