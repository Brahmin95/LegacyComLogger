using MyCompany.Logging.Abstractions;
using System;

namespace MyCompany.Logging.NLogProvider
{
    public static class NLogInitializer
    {
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
                LogManager.SetAbstractedContextProperty("session.id", correlationId);

                _isInitialized = true;
            }
        }

        public static void ConfigureDotNetContext()
        {
            EnsureInitialized();
            LogManager.SetAbstractedContextProperty("labels.app_type", ".NET");
            try
            {
                LogManager.SetAbstractedContextProperty("service.name", AppDomain.CurrentDomain.FriendlyName);
            }
            catch { /* resilient */ }
        }

        public static void ConfigureVb6Context()
        {
            EnsureInitialized();
            LogManager.SetAbstractedContextProperty("labels.app_type", "VB6");
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    LogManager.SetAbstractedContextProperty("service.name", process.MainModule.ModuleName);
                }
            }
            catch { /* resilient */ }
        }
    }
}