using System;

namespace VanillaNLogTest
{
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            Console.WriteLine("Logging with the NLog v4 compatible ecosystem...");
            Logger.Info("This will be ECS JSON from NLog v4.");
            Console.WriteLine("Log message sent. Check console for JSON output.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}