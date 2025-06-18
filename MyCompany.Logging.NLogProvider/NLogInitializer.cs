using System;
using System.Diagnostics;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// Encapsulates all NLog-specific global context setup logic.
    /// This class is internal to the provider and is responsible for establishing
    /// the session-wide and application-specific context for every log message.
    /// </summary>
    public static class NLogInitializer
    {
        // A constant for the environment variable name to ensure consistency.
        private const string CorrelationIdEnvVar = "MYAPP_SESSION_CORRELATION_ID";

        // Static fields to ensure the initialization logic runs only once per process.
        private static bool _isSessionContextInitialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// This is the core, thread-safe method that establishes the cross-process
        //  session correlation ID. It checks for an environment variable, and if not found,
        /// creates a new ID and sets the variable for child processes to inherit.
        /// </summary>
        private static void EnsureSessionContextInitialized()
        {
            // Use a double-check lock pattern for performance and thread safety.
            if (_isSessionContextInitialized) return;

            lock (_lock)
            {
                if (_isSessionContextInitialized) return;

                // Attempt to read the correlation ID from the parent process's environment.
                string correlationId = Environment.GetEnvironmentVariable(CorrelationIdEnvVar);

                // If it doesn't exist, this is the root process for this session.
                if (string.IsNullOrEmpty(correlationId))
                {
                    // Create a new, unique ID for this entire user workflow.
                    correlationId = Guid.NewGuid().ToString("N");

                    // Set it as a process-scoped environment variable so any child
                    // processes launched from here will inherit it.
                    Environment.SetEnvironmentVariable(CorrelationIdEnvVar, correlationId);
                }

                // Add the ID to NLog's Mapped Diagnostics Logical Context (MDLC).
                // It will now be attached to every log event from this process.
                NLog.MappedDiagnosticsLogicalContext.Set("sessionCorrelationId", correlationId);

                _isSessionContextInitialized = true;
            }
        }

        /// <summary>
        /// Configures the global logging context specifically for a .NET application.
        /// </summary>
        public static void ConfigureDotNetContext()
        {
            // First, ensure the shared session ID is set.
            EnsureSessionContextInitialized();

            // Then, set the context specific to .NET applications.
            NLog.MappedDiagnosticsLogicalContext.Set("appType", ".NET");
            try
            {
                // AppDomain.CurrentDomain.FriendlyName is a reliable way to get the .exe name.
                NLog.MappedDiagnosticsLogicalContext.Set("appName", AppDomain.CurrentDomain.FriendlyName);
            }
            catch (Exception ex)
            {
                // Be resilient. If this fails, logging should not crash the app.
                NLog.Common.InternalLogger.Warn(ex, "Failed to get appName for .NET context.");
            }
        }

        /// <summary>
        /// Configures the global logging context specifically for a VB6 application (via the COM bridge).
        /// </summary>
        public static void ConfigureVb6Context()
        {
            // First, ensure the shared session ID is set.
            EnsureSessionContextInitialized();

            // Then, set the context specific to VB6 applications.
            NLog.MappedDiagnosticsLogicalContext.Set("appType", "VB6");
            try
            {
                // For a DLL hosted in a VB6 process, getting the current process's
                // module name is the correct way to identify the host .exe.
                using (var process = Process.GetCurrentProcess())
                {
                    NLog.MappedDiagnosticsLogicalContext.Set("appName", process.MainModule.ModuleName);
                }
            }
            catch (Exception ex)
            {
                // Be resilient.
                NLog.Common.InternalLogger.Warn(ex, "Failed to get appName for VB6 context.");
            }
        }
    }
}