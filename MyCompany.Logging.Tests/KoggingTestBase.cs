using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using System;
using System.Reflection;

namespace MyCompany.Logging.Tests
{
    public abstract class LoggingTestBase : IDisposable
    {
        protected const string TestCorrelationIdEnvVar = "MYAPP_SESSION_CORRELATION_ID";

        protected LoggingTestBase()
        {
            ResetAllStaticState();
        }

        private void ResetAllStaticState()
        {
            // Reset our own static LogManager
            var logManagerFactoryField = typeof(LogManager).GetField("_factory", BindingFlags.NonPublic | BindingFlags.Static);
            logManagerFactoryField?.SetValue(null, null);

            // Reset NLog's static configuration
            NLog.LogManager.Configuration = null;

            // --- THIS IS THE DEFINITIVE FIX ---
            // The field name must exactly match the one in NLogInitializer.cs.
            // It was `_isInitialized` and it should have been `_isSessionContextInitialized`.
            var initializerIsInitializedField = typeof(NLogInitializer).GetField("_isSessionContextInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            if (initializerIsInitializedField == null)
            {
                // This will throw an exception if we ever rename the field again, preventing this bug.
                throw new InvalidOperationException("Could not find the private static field '_isSessionContextInitialized' in NLogInitializer. It may have been renamed.");
            }
            initializerIsInitializedField.SetValue(null, false); // Set it back to false

            // Clear NLog's context
            NLog.MappedDiagnosticsLogicalContext.Clear();

            // Clean up the environment variable
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, null);
        }

        public void Dispose()
        {
            ResetAllStaticState();
        }
    }
}