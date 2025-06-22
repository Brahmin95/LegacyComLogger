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
// THE FIX: Create unambiguous aliases for our framework's types to resolve conflicts with NLog's types.
using OurILogger = MyCompany.Logging.Abstractions.ILogger;
using OurLogManager = MyCompany.Logging.Abstractions.LogManager;


namespace MyCompany.Logging.Benchmarks
{
    // Configuration to produce clean, categorized results.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [SimpleJob(RuntimeMoniker.Net462)]
    [MarkdownExporterAttribute.GitHub] // Produces a nice markdown file in the results folder
    public class PerformanceBenchmarks
    {
        // THE FIX: Use the OurILogger alias for the field declaration.
        private OurILogger _ourLogger;
        private NLog.ILogger _nlogDirectLogger;
        private LoggingComBridge _comBridge;
        private Dictionary<string, object> _testProperties;

        private const string TestMessage = "This is a test log message.";
        private const string VbCls = "Benchmark.cls";
        private const string VbMethod = "TestMethod";

        // Use a parameter to run each benchmark for 5, 50, and 500 log events.
        [Params(5, 50, 500)]
        public int LogCount;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // --- Setup NLog directly ---
            var config = new NLogConfig.LoggingConfiguration();
            var nullTarget = new NullTarget("blackhole");
            config.AddRuleForAllLevels(nullTarget);
            NLog.LogManager.Configuration = config;
            _nlogDirectLogger = NLog.LogManager.GetLogger("DirectNLog");

            // --- Setup Our Framework ---
            // THE FIX: Use the OurLogManager alias.
            OurLogManager.Initialize(AppRuntime.DotNet);
            _ourLogger = OurLogManager.GetLogger("OurFramework");
            _comBridge = new LoggingComBridge();

            // --- Setup Test Properties ---
            _testProperties = new Dictionary<string, object>
            {
                { "Prop1", "Value1" }, { "Prop2", 123 }, { "Prop3", true },
                { "Prop4", DateTime.UtcNow }, { "Prop5", Guid.NewGuid() }
            };
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
        public void Framework_VB6_Simple()
        {
            for (int i = 0; i < LogCount; i++)
                _comBridge.Info(VbCls, VbMethod, TestMessage);
        }

        // ====== SCENARIO 2: SIMPLE LOGGING (WITH 5 PROPERTIES) ======

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Simple_WithProps")]
        public void Baseline_WithProperties()
        {
            for (int i = 0; i < LogCount; i++)
            {
                var logEvent = new LogEventInfo(LogLevel.Info, "DirectNLog", TestMessage);
                foreach (var prop in _testProperties)
                    logEvent.Properties[prop.Key] = prop.Value;
                _nlogDirectLogger.Log(logEvent);
            }
        }

        [Benchmark]
        [BenchmarkCategory("Simple_WithProps")]
        public void Framework_DotNet_WithProperties()
        {
            for (int i = 0; i < LogCount; i++)
                _ourLogger.Info(TestMessage, _testProperties);
        }

        [Benchmark]
        [BenchmarkCategory("Simple_WithProps")]
        public void Framework_VB6_WithProperties()
        {
            for (int i = 0; i < LogCount; i++)
                _comBridge.Info(VbCls, VbMethod, TestMessage, _testProperties);
        }

        // ====== SCENARIO 3: LOGGING WITHIN A SINGLE TRACE ======

        [Benchmark]
        [BenchmarkCategory("Trace_Single")]
        public void Framework_DotNet_InTrace()
        {
            // THE FIX: Use the OurLogManager alias.
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

        // ====== SCENARIO 4: LOGGING WITHIN A TRACE AND 2 SPANS ======

        [Benchmark]
        [BenchmarkCategory("Trace_MultiSpan")]
        public void Framework_DotNet_InTraceWithSpans()
        {
            // THE FIX: Use the OurLogManager alias.
            OurLogManager.Tracer.Trace("DotNetTrace", TxType.Process, () =>
            {
                // First half of the logs in the first span
                OurLogManager.Tracer.Trace("Span1", TxType.Process, () =>
                {
                    for (int i = 0; i < LogCount / 2; i++)
                        _ourLogger.Info(TestMessage);
                });
                // Second half of the logs in the second span
                OurLogManager.Tracer.Trace("Span2", TxType.Process, () =>
                {
                    for (int i = 0; i < (LogCount - LogCount / 2); i++)
                        _ourLogger.Info(TestMessage);
                });
            });
        }

        [Benchmark]
        [BenchmarkCategory("Trace_MultiSpan")]
        public void Framework_VB6_InTraceWithSpans()
        {
            using (_comBridge.BeginTrace("Vb6Trace", TxType.Process))
            {
                using (_comBridge.BeginSpan("Span1", TxType.Process))
                {
                    for (int i = 0; i < LogCount / 2; i++)
                        _comBridge.Info(VbCls, VbMethod, TestMessage);
                }
                using (_comBridge.BeginSpan("Span2", TxType.Process))
                {
                    for (int i = 0; i < (LogCount - LogCount / 2); i++)
                        _comBridge.Info(VbCls, VbMethod, TestMessage);
                }
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