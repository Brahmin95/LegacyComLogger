using MyCompany.Logging.Abstractions;
using System;
using System.Diagnostics;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// Provides static methods to perform initial context configuration for the logging provider.
    /// This class is called by the LogManager during initialization.
    /// </summary>
    public static class NLogInitializer
    {
        private const string CorrelationIdEnvVar = "MYAPP_SESSION_ID";
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Ensures that one-time, process-level initialization logic is run only once.
        /// Specifically, it creates and caches a 'session.id' for the lifetime of the process.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_isInitialized) return;
            lock (_lock)
            {
                if (_isInitialized) return;

                // We use an environment variable to establish a session ID. This is crucial in the
                // VB6/COM world, as multiple LoggingComBridge objects could be created and destroyed
                // within the same process, but they should all share the same session ID.
                string correlationId = Environment.GetEnvironmentVariable(CorrelationIdEnvVar);
                if (string.IsNullOrEmpty(correlationId))
                {
                    correlationId = Guid.NewGuid().ToString("N");
                    Environment.SetEnvironmentVariable(CorrelationIdEnvVar, correlationId);
                }
                LogManager.SetAbstractedContextProperty("session.id", correlationId);

                _isInitialized = true;
            }
        }

        /// <summary>
        /// Configures the logging context for a standard .NET application environment.
        /// </summary>
        public static void ConfigureDotNetContext()
        {
            EnsureInitialized();
            LogManager.SetAbstractedContextProperty("labels.app_type", ".NET");
            try
            {
                // For .NET apps, the AppDomain's friendly name is a reliable way to get the EXE name.
                LogManager.SetAbstractedContextProperty("service.name", AppDomain.CurrentDomain.FriendlyName);
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Warn("Failed to get AppDomain.CurrentDomain.FriendlyName.", ex);
            }
        }

        /// <summary>
        /// Configures the logging context for a VB6 application environment.
        /// </summary>
        public static void ConfigureVb6Context()
        {
            EnsureInitialized();
            LogManager.SetAbstractedContextProperty("labels.app_type", "VB6");
            try
            {
                // For VB6 apps (and other unmanaged hosts), we must inspect the current process
                // to determine the name of the executable.
                using (var process = Process.GetCurrentProcess())
                {
                    LogManager.SetAbstractedContextProperty("service.name", process.MainModule.ModuleName);
                }
            }
            catch (Exception ex)
            {
                LogManager.InternalLogger.Warn("Failed to get current process MainModule.ModuleName.", ex);
            }
        }
    }
}