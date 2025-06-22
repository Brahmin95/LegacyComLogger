using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.Interop;
using System;
using System.Collections.Generic;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// Contains unit tests specifically for the ambient context scope management
    /// (BeginTrace/BeginSpan) of the LoggingComBridge.
    /// </summary>
    public class ComBridgeScopeTests : LoggingTestBase
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IInternalLogger> _mockInternalLogger;
        private readonly LoggingComBridge _bridge;

        public ComBridgeScopeTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockInternalLogger = new Mock<IInternalLogger>();
            var mockFactory = new Mock<ILoggerFactory>();

            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, _mockInternalLogger.Object);

            _bridge = new LoggingComBridge();
        }

        /// <summary>
        /// Test Case 1: Verifies a transaction can start and stop, and that context
        /// is correctly applied only within the scope.
        /// </summary>
        [Fact]
        public void BeginTrace_WhenCalled_AddsContextAndCleansUpCorrectly()
        {
            // Arrange & Act
            using (var trace = _bridge.BeginTrace("TestTrace", "test.type"))
            {
                _bridge.Info("TestFile", "InTrace", "Message inside trace");
            }
            _bridge.Info("TestFile", "OutOfTrace", "Message outside trace");

            // Assert
            _mockLogger.Verify(log => log.Info("Message inside trace",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("trace.id") &&
                    d.ContainsKey("transaction.id") &&
                    d["trace.id"] == d["transaction.id"]
                )
            ), Times.Once);

            _mockLogger.Verify(log => log.Info("Message outside trace",
                It.Is<Dictionary<string, object>>(d => !d.ContainsKey("trace.id"))
            ), Times.Once);
        }

        /// <summary>
        /// Test Case 2: Verifies that nested spans correctly inherit the parent trace
        /// context and that sequential spans are handled correctly.
        /// </summary>
        [Fact]
        public void BeginSpan_WithActiveTrace_CorrectlyNestsAndInheritsContext()
        {
            // Arrange
            string traceId = null;
            string transactionId = null;
            string span1Id = null;

            // Act
            using (var trace = _bridge.BeginTrace("TestTrace", "test.type"))
            {
                _mockLogger.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                    .Callback<string, Dictionary<string, object>>((msg, d) => {
                        if (msg == "Message inside Span 1")
                        {
                            traceId = (string)d["trace.id"];
                            transactionId = (string)d["transaction.id"];
                            span1Id = (string)d["span.id"];
                        }
                    });

                using (var span1 = _bridge.BeginSpan("Span1", "test.span"))
                {
                    _bridge.Info("TestFile", "InSpan1", "Message inside Span 1");
                }

                using (var span2 = _bridge.BeginSpan("Span2", "test.span"))
                {
                    _bridge.Info("TestFile", "InSpan2", "Message inside Span 2");
                }
            }

            // Assert
            Assert.NotNull(traceId);
            Assert.Equal(traceId, transactionId);

            _mockLogger.Verify(log => log.Info("Message inside Span 2",
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["trace.id"] == traceId &&
                    (string)d["transaction.id"] == transactionId &&
                    d.ContainsKey("span.id") &&
                    (string)d["span.id"] != span1Id
                )
            ), Times.Once);
        }

        /// <summary>
        /// Test Case 3: Verifies the resilient behavior where starting a span without an
        /// active trace automatically creates a parent trace.
        /// </summary>
        [Fact]
        public void BeginSpan_WithoutActiveTrace_AutomaticallyCreatesParentTrace()
        {
            // Arrange & Act
            using (var spanAsTrace = _bridge.BeginSpan("OrphanSpan", "test.span"))
            {
                _bridge.Info("TestFile", "InOrphanSpan", "Message inside orphan span", null);
            }

            // Assert
            _mockLogger.Verify(log => log.Info("Message inside orphan span",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("trace.id") &&
                    d.ContainsKey("transaction.id") &&
                    d["trace.id"] == d["transaction.id"] &&
                    !d.ContainsKey("span.id")
                )
            ), Times.Once);

            _mockInternalLogger.Verify(log => log.Debug(It.Is<string>(s => s.Contains("Automatically creating a new parent trace")), null), Times.Once);
        }

        /// <summary>
        /// Test Case 4: Verifies the resilient behavior for out-of-order disposal,
        /// ensuring that disposing a parent before a child does not corrupt the context stack.
        /// </summary>
        [Fact]
        public void DisposeParentTrace_BeforeChildSpan_PreventsContextCorruption()
        {
            // Arrange
            var trace = _bridge.BeginTrace("ParentTrace", "test.type");
            var span = _bridge.BeginSpan("ChildSpan", "test.span");

            // Act
            (trace as IDisposable)?.Dispose();
            _bridge.Info("TestFile", "AfterParentDispose", "Message after parent disposed", null);
            (span as IDisposable)?.Dispose();

            // Assert
            _mockLogger.Verify(log => log.Info("Message after parent disposed",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("trace.id") &&
                    d.ContainsKey("span.id")
                )
            ), Times.Once);

            _mockInternalLogger.Verify(log => log.Warn(It.Is<string>(s => s.Contains("Out-of-order disposal detected")), null), Times.Once);
        }

        /// <summary>
        /// Test Case 5: Verifies the resilient behavior where starting a "trace" inside
        /// an already active trace simply behaves like starting a new span.
        /// </summary>
        [Fact]
        public void BeginTrace_WhenTraceIsActive_BehavesAsSpan()
        {
            // Arrange
            string outerTraceId = null;
            // Set up the mock callback BEFORE any action is taken.
            _mockLogger.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((msg, d) => {
                    if (d.ContainsKey("trace.id"))
                    {
                        // We capture the first trace ID we see.
                        if (outerTraceId == null)
                        {
                            outerTraceId = (string)d["trace.id"];
                        }
                    }
                });

            // Act
            using (var trace1 = _bridge.BeginTrace("Trace1", "type1"))
            {
                // The BeginTrace call itself will log and trigger the callback, setting outerTraceId.
                Assert.NotNull(outerTraceId);

                using (var trace2AsSpan = _bridge.BeginTrace("Trace2_AsSpan", "type2"))
                {
                    _bridge.Info("TestFile", "InNestedTrace", "Message in nested trace");
                }
            }

            // Assert
            _mockLogger.Verify(log => log.Info("Message in nested trace",
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["trace.id"] == outerTraceId &&
                    d.ContainsKey("span.id")
                )
            ), Times.Once);
        }

        /// <summary>
        /// Verifies that the ErrorHandler method correctly creates a VBErrorException and
        /// that the ambient context is applied to the error log.
        /// </summary>
        [Fact]
        public void ErrorHandler_WithinTrace_CreatesExceptionAndHasAmbientContext()
        {
            // Arrange
            using (var trace = _bridge.BeginTrace("ErrorTest", "test"))
            {
                // Act
                _bridge.ErrorHandler("File.cls", "Method", "Operation failed", "Disk Full", 76, "DAO", 123);
            }

            // Assert
            _mockLogger.Verify(log => log.Error(
                "Operation failed",
                // Assert that the exception is the correct type and has the correct data.
                It.Is<VBErrorException>(ex => ex.VbErrorNumber == 76 && ex.VbLineNumber == 123),
                // Assert that the properties dictionary still contains the ambient trace ID.
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("trace.id"))
            ), Times.Once);
        }
    }
}