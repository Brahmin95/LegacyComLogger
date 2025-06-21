using Moq;
using MyCompany.Logging.NLogProvider;
using NLog.Config;
using System.Collections.Generic;
using Xunit;
using NLogManager = NLog.LogManager;

namespace MyCompany.Logging.Tests
{
    public class NLogProviderTests : LoggingTestBase
    {
        private readonly UnitTestTarget _testTarget;
        private readonly NLog.ILogger _nlogInstance;
        private readonly Mock<IApmAgentWrapper> _mockApmWrapper;

        public NLogProviderTests()
        {
            // Setup NLog to capture events for inspection in every test
            var config = new LoggingConfiguration();
            _testTarget = new UnitTestTarget();
            config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, _testTarget);
            NLogManager.Configuration = config;
            _nlogInstance = NLogManager.GetLogger("TestLogger");

            // Create the mock wrapper for APM
            _mockApmWrapper = new Mock<IApmAgentWrapper>();
        }

        [Fact]
        public void Log_WithVb6CallSiteProperties_EnrichesWithNLogCallSiteKeysAndCleansUp()
        {
            // ARRANGE
            // Directly instantiate the SUT (System Under Test) with its dependencies
            var logger = new NLogLogger(_nlogInstance, _mockApmWrapper.Object);
            var vbProperties = new Dictionary<string, object>
            {
                { "vbCodeFile", "Customer.cls" },
                { "vbMethodName", "SaveCustomer" }
            };

            // ACT
            logger.Info("VB6 call site test", vbProperties);

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal("Customer.cls", logEvent.Properties["callsite-filename"]);
            Assert.Equal("SaveCustomer", logEvent.Properties["callsite"]);
            Assert.False(logEvent.Properties.ContainsKey("vbCodeFile")); // Verify original was removed
            Assert.False(logEvent.Properties.ContainsKey("vbMethodName")); // Verify original was removed
        }

        [Fact]
        public void Log_WhenApmTransactionIsActive_EnrichesWithApmCorrelationIds()
        {
            // ARRANGE
            // Setup the mock wrapper to simulate an active APM transaction
            _mockApmWrapper.Setup(w => w.GetCurrentTraceId()).Returns("test-trace-id");
            _mockApmWrapper.Setup(w => w.GetCurrentTransactionId()).Returns("test-txn-id");
            _mockApmWrapper.Setup(w => w.GetCurrentSpanId()).Returns("test-span-id");
            var logger = new NLogLogger(_nlogInstance, _mockApmWrapper.Object);

            // ACT
            logger.Info("This should be correlated");

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal("test-trace-id", logEvent.Properties["trace.id"]);
            Assert.Equal("test-txn-id", logEvent.Properties["transaction.id"]);
            Assert.Equal("test-span-id", logEvent.Properties["span.id"]);
        }

        [Fact]
        public void Log_WhenApmTransactionIsInActive_DoesNotAddApmProperties()
        {
            // ARRANGE
            // The mock wrapper will return null by default, simulating an inactive transaction
            var logger = new NLogLogger(_nlogInstance, _mockApmWrapper.Object);

            // ACT
            logger.Info("This should not be correlated");

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.False(logEvent.Properties.ContainsKey("trace.id"));
            Assert.False(logEvent.Properties.ContainsKey("transaction.id"));
            Assert.False(logEvent.Properties.ContainsKey("span.id"));
        }

        [Fact]
        public void Log_WithBothVb6AndApmContext_EnrichesWithAllFieldsCorrectly()
        {
            // ARRANGE
            _mockApmWrapper.Setup(w => w.GetCurrentTraceId()).Returns("apm-trace-id");
            _mockApmWrapper.Setup(w => w.GetCurrentTransactionId()).Returns("apm-txn-id");
            var logger = new NLogLogger(_nlogInstance, _mockApmWrapper.Object);

            var vbProperties = new Dictionary<string, object>
            {
                { "vbCodeFile", "Orders.frm" },
                { "vbMethodName", "btnSubmit_Click" },
                { "customProp", "ABC" }
            };

            // ACT
            logger.Info("Combined enrichment test", vbProperties);

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];

            // Verify all enrichments and original properties are present and correct
            Assert.Equal("apm-trace-id", logEvent.Properties["trace.id"]);
            Assert.Equal("apm-txn-id", logEvent.Properties["transaction.id"]);
            Assert.Equal("Orders.frm", logEvent.Properties["callsite-filename"]);
            Assert.Equal("btnSubmit_Click", logEvent.Properties["callsite"]);
            Assert.Equal("ABC", logEvent.Properties["customProp"]);
            Assert.False(logEvent.Properties.ContainsKey("vbCodeFile")); // Verify cleanup
        }

        [Fact]
        public void Info_WithPropertyDictionary_LogsComplexKeyNamesCorrectly()
        {
            // ARRANGE
            var logger = new NLogLogger(_nlogInstance, _mockApmWrapper.Object);
            var properties = new Dictionary<string, object>
            {
                { "user.id", "user-555" },
                { "transaction.id", "manual-txn-id" } // A manually supplied property
            };

            // ACT
            logger.Info("Processing complex event", properties);

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal("user-555", logEvent.Properties["user.id"]);
            // The manually supplied property should be present
            Assert.Equal("manual-txn-id", logEvent.Properties["transaction.id"]);
        }
    }
}