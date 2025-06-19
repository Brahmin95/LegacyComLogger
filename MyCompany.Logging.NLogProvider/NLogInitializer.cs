using System;

namespace MyCompany.Logging.NLogProvider
{
    public static class NLogInitializer
    {
        // Use the official ECS field name for the environment variable.
        private const string CorrelationIdEnvVar = "MYAPP_SESSION_ID";
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        private static void EnsureInitialized()
        {
            if (_isInitialized) return;
            lock (_lock)
            {
                if (_isInitialized) return;

                string correlationId = Environment.GetEnvironmentVariable(CorrelationIdEnvVar);
                if (string.IsNullOrEmpty(correlationId))
                {
                    correlationId = Guid.NewGuid().ToString("N");
                    Environment.SetEnvironmentVariable(CorrelationIdEnvVar, correlationId);
                }
                // FIXED: Use the ECS key 'session.id'
                NLog.MappedDiagnosticsLogicalContext.Set("session.id", correlationId);

                _isInitialized = true;
            }
        }

        public static void ConfigureDotNetContext()
        {
            EnsureInitialized();
            // FIXED: Use the key 'labels.app_type' for custom metadata
            NLog.MappedDiagnosticsLogicalContext.Set("labels.app_type", ".NET");
            try
            {
                // FIXED: Use the key 'service.name' for the application name
                NLog.MappedDiagnosticsLogicalContext.Set("service.name", AppDomain.CurrentDomain.FriendlyName);
            }
            catch { /* resilient */ }
        }

        public static void ConfigureVb6Context()
        {
            EnsureInitialized();
            // FIXED: Use the key 'labels.app_type' for custom metadata
            NLog.MappedDiagnosticsLogicalContext.Set("labels.app_type", "VB6");
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    // FIXED: Use the key 'service.name' for the application name
                    NLog.MappedDiagnosticsLogicalContext.Set("service.name", process.MainModule.ModuleName);
                }
            }
            catch { /* resilient */ }
        }
    }
}