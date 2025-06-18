using System;
using System.Collections.Generic;

namespace MyCompany.Logging.Abstractions
{
    public interface ILoggerFactory
    {
        ILogger GetLogger(string name);
    }

    public static class LogManager
    {
        private static ILoggerFactory _factory;
        private static readonly object _lock = new object();

        public static bool IsInitialized => _factory != null;

        public static void Initialize(ILoggerFactory factory)
        {
            if (_factory != null) return;
            lock (_lock)
            {
                if (_factory != null) return;
                _factory = factory;
            }
        }

        public static ILogger GetLogger(string name)
        {
            return _factory?.GetLogger(name) ?? new NullLogger();
        }

        public static ILogger GetCurrentClassLogger()
        {
            string className = new System.Diagnostics.StackFrame(1, false).GetMethod().DeclaringType.FullName;
            return GetLogger(className);
        }

        private class NullLogger : ILogger
        {
            public void Trace(string mt, params object[] a) { }
            public void Debug(string mt, params object[] a) { }
            public void Info(string mt, params object[] a) { }
            public void Warn(string mt, params object[] a) { }
            public void Error(Exception ex, string mt, params object[] a) { }
            public void Fatal(Exception ex, string mt, params object[] a) { }
            public void Trace(string m, Dictionary<string, object> p = null) { }
            public void Debug(string m, Dictionary<string, object> p = null) { }
            public void Info(string m, Dictionary<string, object> p = null) { }
            public void Warn(string m, Dictionary<string, object> p = null) { }
            public void Error(string m, Exception ex = null, Dictionary<string, object> p = null) { }
            public void Fatal(string m, Exception ex = null, Dictionary<string, object> p = null) { }
        }
    }
}