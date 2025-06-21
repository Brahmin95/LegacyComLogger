using MyCompany.Logging.Abstractions;

namespace MyCompany.Logging.NLogProvider
{
    public class NLogLoggerFactory : ILoggerFactory
    {
        public NLogLoggerFactory()
        {
            LogManager.SetContextProperty = (key, value) => NLog.MappedDiagnosticsLogicalContext.Set(key, value);
        }

        public ILogger GetLogger(string name)
        {
            // MODIFIED: The factory is now responsible for creating and injecting
            // the dependencies for the NLogLogger. It provides the "real" wrapper
            // for the production application.
            var nlogInstance = NLog.LogManager.GetLogger(name);
            var apmWrapper = new ElasticApmAgentWrapper();
            return new NLogLogger(nlogInstance, apmWrapper);
        }
    }
}