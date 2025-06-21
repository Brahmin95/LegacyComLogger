using MyCompany.Logging.Abstractions;
using System;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    static class Program
    {
        private static ILogger logger;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            InitialiseLogging();
            logger = LogManager.GetCurrentClassLogger();
            logger.Info("Application started");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            logger.Info("Application Exiting");
        }

        private static ILogger InitialiseLogging()
        {
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);

            ILogger _log = LogManager.GetLogger("Program");

            if (!LogManager.IsInitialized)
            {
                _log.Warn("Application starting with main logging system disabled. Please check internal logs for fatal errors.");
            }
            else
            {
                _log.Info("Application startup complete. Logging is active.");
            }

            return _log;
        }
    }
}
