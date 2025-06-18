using MyCompany.Logging.Abstractions;

namespace MyCompany.Logging.NLogProvider
{
    public class NLogLoggerFactory : ILoggerFactory
    {
        public NLogLoggerFactory(string contextType = "NET")
        {
            if (contextType == "VB6")
            {
                NLogInitializer.ConfigureVb6Context();
            }
            else
            {
                NLogInitializer.ConfigureDotNetContext();
            }
        }

        public ILogger GetLogger(string name)
        {
            return new NLogLogger(NLog.LogManager.GetLogger(name));
        }
    }
}