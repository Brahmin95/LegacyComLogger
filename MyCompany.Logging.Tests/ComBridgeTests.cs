using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

namespace MyCompany.Logging.Tests
{
    public class ComBridgeTests : LoggingTestBase
    {
        [Fact]
        public void Constructor_WhenCalled_InitializesVb6Context()
        {
            // Arrange & Act
            var bridge = new LoggingComBridge(); // This now calls NLogInitializer.ConfigureVb6Context()

            // Assert
            // We check the MDLC state to confirm the constructor did its job.
            Assert.Equal("VB6", NLog.MappedDiagnosticsLogicalContext.Get("labels.app_type"));
            Assert.NotNull(NLog.MappedDiagnosticsLogicalContext.Get("service.name"));
        }

        [Fact]
        public void CreatePropertiesWithTransactionId_ReturnsPopulatedDictionary()
        {
            // Arrange
            var bridge = new LoggingComBridge();

            // Act
            dynamic props = bridge.CreatePropertiesWithTransactionId();

            // Assert
            // Verify it contains the new ECS-compliant key.
            Assert.NotNull(props.Item("transaction.id"));
        }

        [Fact]
        public void Info_WithComProperties_PassesCorrectDataToUnderlyingLogger()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            LogManager.Initialize(mockFactory.Object);

            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            props.Add("customerId", 12345);

            // Act
            bridge.Info("Test.cls", "TestMethod", "Test message", props);

            // Assert
            // Check for the new ECS-compliant property names.
            mockLogger.Verify(log => log.Info(
                "Test message",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("vbCodeFile") && d["vbCodeFile"].ToString() == "Test.cls" &&
                    d.ContainsKey("vbMethodName") && d["vbMethodName"].ToString() == "TestMethod" &&
                    d.ContainsKey("transaction.id") && // <-- UPDATED KEY
                    d.ContainsKey("customerId") && (int)d["customerId"] == 12345
                )
            ), Times.Once);
        }

        // Mock COM object for testing sanitization
        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.AutoDual)]
        public class MockableComObject
        {
            public string ToLogString() => "ID=555,Name=MockObject";
        }

        [Fact]
        public void SanitizeValue_HandlesAllDataTypesGracefully()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            LogManager.Initialize(mockFactory.Object);

            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreateProperties();

            props.Add("aString", "hello");
            props.Add("anInteger", 123);
            props.Add("goodComObject", new MockableComObject());
            props.Add("badComObject", new object()); // A standard object that doesn't have ToLogString()

            // Act
            bridge.Info("Test.cls", "TestMethod", "Data type test", props);

            // Assert
            // This test logic remains the same as it tests the sanitization behavior, which hasn't changed.
            mockLogger.Verify(log => log.Info(
                It.IsAny<string>(),
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["aString"] == "hello" &&
                    (int)d["anInteger"] == 123 &&
                    (string)d["goodComObject"] == "ID=555,Name=MockObject" &&
                    (string)d["badComObject"] == "System.Object" // Correctly falls back to ToString()
                )
            ), Times.Once);
        }
    }
}