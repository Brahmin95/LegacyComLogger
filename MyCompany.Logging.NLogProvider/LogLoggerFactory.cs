using MyCompany.Logging.Abstractions;
using System.IO;
using System.Reflection;

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
        /// into NLog's global context and robustly loads NLog extensions.
        /// </summary>
        public NLogLoggerFactory()
        {
            // THE FIX: Explicitly tell NLog where to find its extension assemblies.
            // This is critical for robust dependency loading when hosted by a native
            // application like VB6, as the default probing paths can be unreliable.
            var providerAssembly = typeof(NLogLoggerFactory).Assembly;
            NLog.LogManager.AddHiddenAssembly(providerAssembly);

            // This delegate connects the abstract LogManager to the concrete NLog implementation.
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