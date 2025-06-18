using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            LogManager.Initialize(new NLogLoggerFactory());
            logger = LogManager.GetCurrentClassLogger();
            logger.Info("Application started");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            logger.Info("Application Exiting");
        }
    }
}
