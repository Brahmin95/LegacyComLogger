using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Configs;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.Interop;
using NLog;
using NLog.Targets;
using NLogConfig = NLog.Config;

namespace MyCompany.Logging.Benchmarks
{
    // Configuration to produce clean, categorized results.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [SimpleJob(RuntimeMoniker.Net462)]
    public class PerformanceBenchmarks
    {
        private ILogger _ourLogger;
        private NLog.ILogger _nlogDirectLogger;
        private LoggingComBridge _comBridge;

        private const string TestMessage = "This is a test log message.";

        [GlobalSetup]
        public void GlobalSetup()
        {
            // --- Setup NLog directly ---
            var config = new NLogConfig.LoggingConfiguration();
            // Use NullTarget to measure pure framework overhead without disk I/O.
            var nullTarget = new NullTarget("blackhole");
            config.AddRuleForAllLevels(nullTarget);
            NLog.LogManager.Configuration = config;
            _nlogDirectLogger = NLog.LogManager.GetLogger("DirectNLog");

            // --- Setup Our Framework ---
            // Our framework will use the same NLog configuration set above.
            LogManager.Initialize(AppRuntime.DotNet);
            _ourLogger = LogManager.GetLogger("OurFramework");
            _comBridge = new LoggingComBridge();
        }

        // === SCENARIO 1: SIMPLE LOGGING ===

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Simple")]
        public void NLog_Direct()
        {
            _nlogDirectLogger.Info(TestMessage);
        }

        [Benchmark]
        [BenchmarkCategory("Simple")]
        public void OurFramework_Simple_Log()
        {
            _ourLogger.Info(TestMessage);
        }


        // === SCENARIO 2: TRACE CONTEXT LOGGING (VB6 SIMULATION) ===

        [Benchmark]
        [BenchmarkCategory("Trace")]
        public void OurFramework_Vb6_Trace_And_Log()
        {
            // This using block simulates the 'Set trace = ... Set trace = Nothing'
            // pattern in VB6, including the cost of creating and disposing the
            // transaction handle and managing the ambient context stack.
            using (_comBridge.BeginTrace("BenchmarkTrace", TxType.Process))
            {
                _comBridge.Info("Benchmark.cls", "TestMethod", TestMessage);
            }
        }
    }
}