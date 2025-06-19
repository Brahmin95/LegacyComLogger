using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using System;
using System.Reflection;
using Xunit; // For [Collection] attribute

namespace MyCompany.Logging.Tests
{
    // --- NEW: Add this attribute to the base class ---
    // This tells xUnit to run all tests in this collection sequentially, not in parallel.
    // This is ESSENTIAL when testing code that relies on static state like LogManager.
    [Collection("SequentialLoggingTests")]
    public abstract class LoggingTestBase : IDisposable
    {
        // Use the same constant as the initializer code for consistency
        protected const string TestCorrelationIdEnvVar = "MYAPP_SESSION_ID";

        protected void LoggingTestBaset()
        {
            // Initial state reset before each test
            ResetAllStaticState();
        }

        public void Dispose()
        {
            // Final state reset after each test
            ResetAllStaticState();
        }

        private void ResetAllStaticState()
        {
            // Reset our own static LogManager by setting its factory to null.
            var logManagerFactoryField = typeof(LogManager).GetField("_factory", BindingFlags.NonPublic | BindingFlags.Static);
            logManagerFactoryField?.SetValue(null, null);

            // Reset NLog's static configuration to a clean slate.
            NLog.LogManager.Configuration = null;

            // --- THIS IS THE DEFINITIVE FIX for NLogInitializer ---
            // The field name must exactly match the one in NLogInitializer.cs.
            var initializerIsInitializedField = typeof(NLogInitializer).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            if (initializerIsInitializedField == null)
            {
                // This will throw an exception if we ever rename the field again, preventing this bug.
                throw new InvalidOperationException("Could not find the private static field '_isInitialized' in NLogInitializer. It may have been renamed.");
            }
            initializerIsInitializedField.SetValue(null, false); // Set it back to false

            // Clear any lingering context in NLog's MDLC.
            NLog.MappedDiagnosticsLogicalContext.Clear();

            // Clean up the environment variable between tests.
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, null);
        }
    }
}