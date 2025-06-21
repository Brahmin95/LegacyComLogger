using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
using System.Collections.Generic;
using Xunit;

namespace MyCompany.Logging.Tests
{
    public class ComBridgeTests : LoggingTestBase
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly LoggingComBridge _bridge;

        public ComBridgeTests()
        {
            _mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);
            _bridge = new LoggingComBridge();
        }

        [Fact]
        public void Log_WithNoActiveTrace_DoesNotAddTraceIds()
        {
            _bridge.Info("Test.cls", "TestMethod", "Message with no context");

            _mockLogger.Verify(log => log.Info(
                "Message with no context",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("vbCodeFile") &&
                    !d.ContainsKey("trace.id") &&
                    !d.ContainsKey("transaction.id")
                )
            ), Times.Once);
        }

        [Fact]
        public void Log_WithinTraceScope_AddsTraceAndTransactionId()
        {
            using (var txn = _bridge.BeginTrace("TestTrace", "test"))
            {
                _bridge.Info("Test.cls", "TestMethod", "Message within trace");
            }

            _mockLogger.Verify(log => log.Info(
                "Message within trace",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("trace.id") &&
                    d.ContainsKey("transaction.id") &&
                    d["trace.id"] == d["transaction.id"]
                )
            ), Times.Once);
        }

        [Fact]
        public void Log_WithinSpanScope_AddsTraceTransactionAndSpanId()
        {
            string traceId = null;
            string transactionId = null;

            using (var trace = _bridge.BeginTrace("TestTrace", "test"))
            {
                _mockLogger.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                   .Callback<string, Dictionary<string, object>>((s, d) =>
                   {
                       if (d.ContainsKey("trace.id")) traceId = (string)d["trace.id"];
                       if (d.ContainsKey("transaction.id")) transactionId = (string)d["transaction.id"];
                   });
                _bridge.Info("Test.cls", "TraceMethod", "Message in trace");

                Assert.NotNull(traceId);
                Assert.NotNull(transactionId);

                using (var span = _bridge.BeginSpan("TestSpan", "test.span"))
                {
                    _bridge.Info("Test.cls", "SpanMethod", "Message within span");
                }
            }

            _mockLogger.Verify(log => log.Info(
                "Message within span",
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["trace.id"] == traceId &&
                    (string)d["transaction.id"] == transactionId &&
                    d.ContainsKey("span.id") &&
                    (string)d["span.id"] != transactionId
                )
            ), Times.Once);
        }

        [Fact]
        public void Log_AfterTraceScopeEnds_DoesNotAddTraceIds()
        {
            using (var txn = _bridge.BeginTrace("TestTrace", "test"))
            {
                _bridge.Info("Test.cls", "TestMethod", "Message within trace");
            }

            _bridge.Info("Test.cls", "AfterMethod", "Message after trace");

            _mockLogger.Verify(log => log.Info(
                "Message within trace",
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("trace.id"))
            ), Times.Once);

            _mockLogger.Verify(log => log.Info(
                "Message after trace",
                It.Is<Dictionary<string, object>>(d => !d.ContainsKey("trace.id"))
            ), Times.Once);
        }
    }
}