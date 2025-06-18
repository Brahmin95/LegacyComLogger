using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
using Scripting;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// Tests the COM Bridge logic.
    /// Inherits from LoggingTestBase to ensure perfect isolation.
    /// </summary>
    public class ComBridgeTests : LoggingTestBase
    {
        // No constructor or Dispose needed here; the base class handles it.

        [Fact]
        public void Constructor_WhenLogManagerNotInitialized_InitializesViaReflection()
        {
            Assert.False(LogManager.IsInitialized);
            var bridge = new LoggingComBridge();
            Assert.True(LogManager.IsInitialized);
        }

        [Fact]
        public void CreatePropertiesWithTransactionId_ReturnsPopulatedDictionary()
        {
            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            Assert.NotNull(props.Item("transactionId"));
        }

        [Fact]
        public void Info_WithComProperties_PassesCorrectDataToUnderlyingLogger()
        {
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            LogManager.Initialize(mockFactory.Object);

            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            props.Add("customerId", 12345);

            bridge.Info("Test.cls", "TestMethod", "Test message", props);

            mockLogger.Verify(log => log.Info(
                "Test message",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("vbCodeFile") && d["vbCodeFile"].ToString() == "Test.cls" &&
                    d.ContainsKey("vbMethodName") && d["vbMethodName"].ToString() == "TestMethod" &&
                    d.ContainsKey("transactionId") &&
                    d.ContainsKey("customerId") && (int)d["customerId"] == 12345
                )
            ), Times.Once);
        }

        [Fact]
        public void SanitizeValue_HandlesAllDataTypesGracefully()
        {
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            LogManager.Initialize(mockFactory.Object);

            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreateProperties();

            props.Add("aString", "hello");
            props.Add("anInteger", 123);
            props.Add("goodComObject", new MockableComObject());
            props.Add("badComObject", new object());

            bridge.Info("Test.cls", "TestMethod", "Data type test", props);

            mockLogger.Verify(log => log.Info(
                It.IsAny<string>(),
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["aString"] == "hello" &&
                    (int)d["anInteger"] == 123 &&
                    (string)d["goodComObject"] == "ID=555,Name=MockObject" &&
                    (string)d["badComObject"] == "System.Object"
                )
            ), Times.Once);
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class MockableComObject
    {
        public string ToLogString() => "ID=555,Name=MockObject";
    }
}