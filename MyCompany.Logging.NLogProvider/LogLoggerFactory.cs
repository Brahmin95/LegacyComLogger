using MyCompany.Logging.Abstractions;

namespace MyCompany.Logging.NLogProvider
{
    public class NLogLoggerFactory : ILoggerFactory
    {
        public NLogLoggerFactory()
        {
            // The constructor is now clean.
            // The central LogManager hooks up the SetContextProperty delegate.
            LogManager.SetContextProperty = (key, value) => NLog.MappedDiagnosticsLogicalContext.Set(key, value);
        }

        public ILogger GetLogger(string name)
        {
            return new NLogLogger(NLog.LogManager.GetLogger(name));
        }
    }
}