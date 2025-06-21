using MyCompany.Logging.Abstractions;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// The NLog implementation of the ILoggerFactory interface. This is the entry point
    /// for creating NLog-backed loggers.
    /// </summary>
    public class NLogLoggerFactory : ILoggerFactory
    {
        /// <summary>
        /// Initializes a new instance of the NLogLoggerFactory class.
        /// It hooks into the LogManager to provide a way to push context properties
        /// into NLog's global context.
        /// </summary>
        public NLogLoggerFactory()
        {
            // This delegate connects the abstract LogManager to the concrete NLog implementation.
            // We use GlobalDiagnosticsContext (GDC) because the properties we set (like service.name
            // and session.id) are global for the entire application process lifetime.
            // This is the modern, non-obsolete replacement for MDLC for this use case.
            LogManager.SetContextProperty = (key, value) => NLog.GlobalDiagnosticsContext.Set(key, value);
        }

        /// <summary>
        /// Gets a logger instance that is backed by NLog.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>An ILogger implementation.</returns>
        public ILogger GetLogger(string name)
        {
            var nlogInstance = NLog.LogManager.GetLogger(name);
            var apmWrapper = new ElasticApmAgentWrapper();
            return new NLogLogger(nlogInstance, apmWrapper);
        }
    }
}