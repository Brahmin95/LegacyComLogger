using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
using MyCompany.Logging.NLogProvider;
using NLog.Config;
using System;
using System.Collections.Generic;
using Xunit;
using NLogManager = NLog.LogManager;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// Contains unit tests for the MyCompany.Logging.NLogProvider project.
    /// These tests verify the provider-specific enrichment logic, such as APM correlation
    /// and the handling of custom VB6 error exceptions.
    /// </summary>
    public class NLogProviderTests : LoggingTestBase
    {
        private readonly UnitTestTarget _testTarget;
        private readonly NLog.ILogger _nlogInstance;
        private readonly Mock<IApmAgentWrapper> _mockApmWrapper;
        private readonly NLogLogger _sut; // System Under Test

        public NLogProviderTests()
        {
            // Setup NLog to capture events for inspection in every test
            var config = new LoggingConfiguration();
            _testTarget = new UnitTestTarget();
            config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, _testTarget);
            NLogManager.Configuration = config;
            _nlogInstance = NLogManager.GetLogger("TestLogger");

            _mockApmWrapper = new Mock<IApmAgentWrapper>();
            _sut = new NLogLogger(_nlogInstance, _mockApmWrapper.Object);
        }

        [Fact]
        public void Log_WhenApmTransactionIsActive_EnrichesWithApmCorrelationIds()
        {
            // Arrange
            _mockApmWrapper.Setup(w => w.GetCurrentTraceId()).Returns("test-trace-id");
            _mockApmWrapper.Setup(w => w.GetCurrentTransactionId()).Returns("test-txn-id");
            _mockApmWrapper.Setup(w => w.GetCurrentSpanId()).Returns("test-span-id");

            // Act
            _sut.Info("This should be correlated");

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal("test-trace-id", logEvent.Properties["trace.id"]);
            Assert.Equal("test-txn-id", logEvent.Properties["transaction.id"]);
            Assert.Equal("test-span-id", logEvent.Properties["span.id"]);
        }

        [Fact]
        public void Log_WithVb6CallSiteProperties_EnrichesWithNLogCallSiteKeysAndCleansUp()
        {
            // Arrange
            var properties = new Dictionary<string, object>
            {
                { "vbCodeFile", "Customer.cls" },
                { "vbMethodName", "SaveCustomer" }
            };

            // Act
            _sut.Info("VB6 call site test", properties);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal("Customer.cls", logEvent.Properties["callsite-filename"]);
            Assert.Equal("SaveCustomer", logEvent.Properties["callsite"]);
            Assert.False(logEvent.Properties.ContainsKey("vbCodeFile"));
            Assert.False(logEvent.Properties.ContainsKey("vbMethodName"));
        }

        /// <summary>
        /// Verifies that when a structured VBErrorException is logged, its rich diagnostic
        /// properties are extracted and added to the log event.
        /// </summary>
        [Fact]
        public void Log_WithStructuredVBErrorException_EnrichesWithVbErrorObjectAndSourceLine()
        {
            // Arrange
            var vbException = new VBErrorException("Disk Full", 76, "DAO.Engine", 123);

            // Act
            _sut.Error("A database operation failed", vbException);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];

            // Assert that the special source.line property was added for Kibana UI integration.
            Assert.True(logEvent.Properties.ContainsKey("source.line"), "The source.line property should be present.");
            Assert.Equal(123, logEvent.Properties["source.line"]);

            // Assert that the custom vb_error object was created and contains the correct data.
            Assert.True(logEvent.Properties.ContainsKey("vb_error"), "The vb_error object should be present.");
            var vbErrorContext = logEvent.Properties["vb_error"] as Dictionary<string, object>;
            Assert.NotNull(vbErrorContext);
            Assert.Equal(76L, vbErrorContext["number"]); // Note: long
            Assert.Equal("DAO.Engine", vbErrorContext["source"]);
        }

        /// <summary>
        /// Verifies that when a simple, logical VBErrorException is logged, the log event
        /// includes the correct error type and a vb_error object with default values.
        /// </summary>
        [Fact]
        public void Log_WithSimpleVBErrorException_HasCorrectTypeAndDefaultVbErrorObject()
        {
            // Arrange
            var logicalException = new VBErrorException("Invalid Customer ID");

            // Act
            _sut.Error("Validation failed", logicalException);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];

            // The exception itself is still present and has the correct type.
            Assert.NotNull(logEvent.Exception);
            Assert.IsType<VBErrorException>(logEvent.Exception);

            // Assert that the vb_error object was added with the default logical error values for consistency.
            Assert.True(logEvent.Properties.ContainsKey("vb_error"), "The vb_error object should be present for logical errors too.");
            var vbErrorContext = logEvent.Properties["vb_error"] as Dictionary<string, object>;
            Assert.NotNull(vbErrorContext);
            Assert.Equal(-1L, vbErrorContext["number"]);
            Assert.Equal("LogicalError", vbErrorContext["source"]);

            // Assert that source.line was NOT added, as it's null for this exception type.
            Assert.False(logEvent.Properties.ContainsKey("source.line"));
        }

        /// <summary>
        /// Verifies that when a standard .NET exception is logged, no VB-specific
        /// enrichment occurs, proving the logic is correctly targeted.
        /// </summary>
        [Fact]
        public void Log_WithStandardException_DoesNotAddVbErrorObject()
        {
            // Arrange
            var standardException = new InvalidOperationException("Something went wrong");

            // Act
            _sut.Error("A standard error occurred", standardException);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];

            // Assert that no VB-specific properties were added.
            Assert.False(logEvent.Properties.ContainsKey("vb_error"));
            Assert.False(logEvent.Properties.ContainsKey("source.line"));
        }

        /// <summary>
        /// Verifies that the NLogInitializer correctly configures context properties
        /// for a VB6 application environment.
        /// </summary>
        [Fact]
        public void NLogInitializer_ConfigureVb6Context_SetsCorrectProperties()
        {
            // Arrange
            NLog.GlobalDiagnosticsContext.Clear();

            // Act
            NLogInitializer.ConfigureVb6Context();

            // Assert
            Assert.Equal("VB6", NLog.GlobalDiagnosticsContext.Get("labels.app_type"));
            Assert.False(string.IsNullOrEmpty(NLog.GlobalDiagnosticsContext.Get("service.name")));
        }
    }
}