using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using System;
using System.Reflection;
using Xunit;

namespace MyCompany.Logging.Tests
{
    [Collection("SequentialLoggingTests")]
    public abstract class LoggingTestBase : IDisposable
    {
        protected const string TestCorrelationIdEnvVar = "MYAPP_SESSION_ID";

        protected LoggingTestBase()
        {
            ResetAllStaticState();
        }

        public void Dispose()
        {
            ResetAllStaticState();
        }

        protected void ResetAllStaticState()
        {
            var logManagerType = typeof(LogManager);

            // Reset factory
            var factoryField = logManagerType.GetField("_factory", BindingFlags.NonPublic | BindingFlags.Static);
            factoryField?.SetValue(null, null);

            // Reset internal logger to a new NullInternalLogger instance
            var internalLoggerProperty = logManagerType.GetProperty("InternalLogger", BindingFlags.Public | BindingFlags.Static);
            var nullInternalLoggerType = logManagerType.GetNestedType("NullInternalLogger", BindingFlags.NonPublic);
            var nullInternalLoggerInstance = Activator.CreateInstance(nullInternalLoggerType);
            internalLoggerProperty?.SetValue(null, nullInternalLoggerInstance, null);

            // Reset NLog's static configuration
            NLog.LogManager.Configuration = null;

            // Reset our NLogInitializer's static state
            var initializerIsInitializedField = typeof(NLogInitializer).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            if (initializerIsInitializedField == null)
            {
                throw new InvalidOperationException("Could not find the private static field '_isInitialized' in NLogInitializer.");
            }
            initializerIsInitializedField.SetValue(null, false);

            NLog.MappedDiagnosticsLogicalContext.Clear();
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, null);
        }

        // Test-specific helper to initialize with mocks without using reflection in every test.
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