using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private static ILogger logger;
        
        public Form1()
        {
            logger = LogManager.GetCurrentClassLogger();
            logger.Info("this is my message");
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            logger.Info("button clicked");
        }
    }
}
