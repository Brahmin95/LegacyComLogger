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
        /// into NLog's MappedDiagnosticsLogicalContext.
        /// </summary>
        public NLogLoggerFactory()
        {
            // This delegate connects the abstract LogManager to the concrete NLog implementation.
            // When LogManager.SetAbstractedContextProperty is called, it will now flow through
            // to NLog's context, making the properties available to layouts.
            LogManager.SetContextProperty = (key, value) => NLog.MappedDiagnosticsLogicalContext.Set(key, value);
        }

        /// <summary>
        /// Gets a logger instance that is backed by NLog.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>An ILogger implementation.</returns>
        public ILogger GetLogger(string name)
        {
            // This is the Composition Root for the logger. It is responsible for creating
            // the logger and all of its dependencies. In production, we new up the "real"
            // dependencies like ElasticApmAgentWrapper. In tests, we can mock them.
            var nlogInstance = NLog.LogManager.GetLogger(name);
            var apmWrapper = new ElasticApmAgentWrapper();
            return new NLogLogger(nlogInstance, apmWrapper);
        }
    }
}