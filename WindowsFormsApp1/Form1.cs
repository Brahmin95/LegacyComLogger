using System;
using System.Windows.Forms;
using MyCompany.Logging;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.Interop;
using Scripting;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private static ILogger logger;
        private static ILoggingComBridge comLogger;
        
        public Form1()
        {
            logger = LogManager.GetCurrentClassLogger();
            comLogger = CreateCOMLogger();
            logger.Info("this is my message");
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            logger.Info(".Net vutton clicked");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var vbProperties = new Dictionary();
            vbProperties.Add("aStringKey", "hello world");
            vbProperties.Add("anIntegerKey", 12345);
            vbProperties.Add("aBooleanKey", true);
            comLogger.Info("avb6Codefile", "MyMethod_click", "Hello from A COM Client", vbProperties);
        }

        private LoggingComBridge CreateCOMLogger()
        {
            var bridge = new LoggingComBridge();
            return bridge;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                throw new Exception("an exception Happened.");
            }
            catch ( Exception ex)
            {
                logger.Error(ex, "Something went Wrong.  {ex}", ex);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            comLogger.Error("MyVB6CodeFile.bas", "MyMethod1", "there was an error");
        }

        private void btnLaunch_Click(object sender, EventArgs e)
        {
            LaunchNewVersion();
        }

        /// <summary>
        /// Launches a new instance of this application from a specified path (or the same path).
        /// </summary>
        /// <param name="newExecutablePath">Optional path to the new version. If null, relaunches the same executable.</param>
        /// <param name="exitCurrent">Whether to shut down the current process after launching the new one.</param>
        public static void LaunchNewVersion(string newExecutablePath = null, bool exitCurrent = false)
        {
            try
            {
                string currentExePath = Assembly.GetExecutingAssembly().Location;
                string launchPath = newExecutablePath ?? currentExePath;

                if (!System.IO.File.Exists(launchPath))
                {
                    throw new FileNotFoundException("Target executable not found", launchPath);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = launchPath,
                    UseShellExecute = true // inherits same elevation (UAC), etc.
                };

                Process.Start(startInfo);

                if (exitCurrent)
                {
                    Environment.Exit(0); // or Application.Exit() for WinForms
                }
            }
            catch (Exception ex)
            {
                // Log or show error message
                logger.Error("Failed to launch new version: ", ex);
                throw;
            }
        }
    }
}

