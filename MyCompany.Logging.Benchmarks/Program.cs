using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.Interop;
using NLog;
using NLog.Targets;
using System;
using System.Collections.Generic;
using NLogConfig = NLog.Config;
// Using alias directives to resolve namespace conflicts.
using OurILogger = MyCompany.Logging.Abstractions.ILogger;
using OurLogManager = MyCompany.Logging.Abstractions.LogManager;


namespace MyCompany.Logging.Benchmarks
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [SimpleJob(RuntimeMoniker.Net462)]
    [MarkdownExporterAttribute.GitHub]
    public class PerformanceBenchmarks
    {
        private OurILogger _ourLogger;
        private NLog.ILogger _nlogDirectLogger;
        private LoggingComBridge _comBridge;
        private Dictionary<string, object> _netProperties;
        private object _comProperties; // Store the COM dictionary as a generic object

        private const string TestMessage = "This is a test log message.";
        private const string VbCls = "Benchmark.cls";
        private const string VbMethod = "TestMethod";

        [Params(5, 50, 500)]
        public int LogCount;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Setup NLog directly
            var config = new NLogConfig.LoggingConfiguration();
            config.AddRuleForAllLevels(new NullTarget("blackhole"));
            NLog.LogManager.Configuration = config;
            _nlogDirectLogger = NLog.LogManager.GetLogger("DirectNLog");

            // Setup Our Framework
            OurLogManager.Initialize(AppRuntime.DotNet);
            _ourLogger = OurLogManager.GetLogger("OurFramework");
            _comBridge = new LoggingComBridge();

            // Setup a standard .NET dictionary for some benchmarks
            _netProperties = new Dictionary<string, object>
            {
                { "Prop1", "Value1" }, { "Prop2", 123 }, { "Prop3", true },
                { "Prop4", DateTime.UtcNow }, { "Prop5", Guid.NewGuid() }
            };

            // THE NEW TEST SETUP: Create and populate a real Scripting.Dictionary
            _comProperties = _comBridge.CreateProperties();
            dynamic scriptDict = _comProperties;
            scriptDict.Add("Prop1", "Value1");
            scriptDict.Add("Prop2", 123);
            scriptDict.Add("Prop3", true);
            scriptDict.Add("Prop4", DateTime.UtcNow);
            scriptDict.Add("Prop5", Guid.NewGuid());
        }

        // ====== SCENARIO 1: SIMPLE LOGGING (NO PROPERTIES) ======

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Simple_NoProps")]
        public void Baseline_Simple()
        {
            for (int i = 0; i < LogCount; i++)
                _nlogDirectLogger.Info(TestMessage);
        }

        [Benchmark]
        [BenchmarkCategory("Simple_NoProps")]
        public void Framework_DotNet_Simple()
        {
            for (int i = 0; i < LogCount; i++)
                _ourLogger.Info(TestMessage);
        }

        [Benchmark]
        [BenchmarkCategory("Simple_NoProps")]
        public void Framework_VbApi_Simple()
        {
            for (int i = 0; i < LogCount; i++)
                _comBridge.Info(VbCls, VbMethod, TestMessage);
        }

        // ====== SCENARIO 2: LOGGING WITH PROPERTIES ======

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Simple_WithProps")]
        public void Baseline_WithProperties()
        {
            for (int i = 0; i < LogCount; i++)
            {
                var logEvent = new LogEventInfo(LogLevel.Info, "DirectNLog", TestMessage);
                foreach (var prop in _netProperties)
                    logEvent.Properties[prop.Key] = prop.Value;
                _nlogDirectLogger.Log(logEvent);
            }
        }

        [Benchmark]
        [BenchmarkCategory("Simple_WithProps")]
        public void Framework_DotNet_WithProperties()
        {
            for (int i = 0; i < LogCount; i++)
                _ourLogger.Info(TestMessage, _netProperties);
        }

        [Benchmark]
        [BenchmarkCategory("Simple_WithProps")]
        // RENAMED: This test is now clearer - it's the VB-style API but with .NET props
        public void Framework_VbApi_WithNetProps()
        {
            for (int i = 0; i < LogCount; i++)
                _comBridge.Info(VbCls, VbMethod, TestMessage, _netProperties);
        }

        [Benchmark]
        [BenchmarkCategory("Simple_WithProps")]
        // NEW BENCHMARK: This is the true test for a VB6 client with a native COM dictionary
        public void Framework_VbApi_WithComProps()
        {
            for (int i = 0; i < LogCount; i++)
                _comBridge.Info(VbCls, VbMethod, TestMessage, _comProperties);
        }

        // ====== SCENARIO 3: LOGGING WITHIN A TRACE ======

        [Benchmark]
        [BenchmarkCategory("Trace_Single")]
        public void Framework_DotNet_InTrace()
        {
            OurLogManager.Tracer.Trace("DotNetTrace", TxType.Process, () =>
            {
                for (int i = 0; i < LogCount; i++)
                    _ourLogger.Info(TestMessage);
            });
        }

        [Benchmark]
        [BenchmarkCategory("Trace_Single")]
        public void Framework_VB6_InTrace()
        {
            using (_comBridge.BeginTrace("Vb6Trace", TxType.Process))
            {
                for (int i = 0; i < LogCount; i++)
                    _comBridge.Info(VbCls, VbMethod, TestMessage);
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<PerformanceBenchmarks>();
        }
    }
}