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
        public void CreatePropertiesWithTransactionId_ReturnsPopulatedDictionary()
        {
            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            Assert.NotNull(props.Item("transaction.id"));
        }

        [Fact]
        public void Info_WithComProperties_PassesCorrectDataToUnderlyingLogger()
        {
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            var mockInternalLogger = new Mock<IInternalLogger>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);

            // Use the test helper to initialize with our mocks
            InitializeWithMocks(mockFactory.Object, mockInternalLogger.Object);

            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            props.Add("customerId", 12345);

            bridge.Info("Test.cls", "TestMethod", "Test message", props);

            mockLogger.Verify(log => log.Info(
                "Test message",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("vbCodeFile") && d["vbCodeFile"].ToString() == "Test.cls" &&
                    d.ContainsKey("vbMethodName") && d["vbMethodName"].ToString() == "TestMethod" &&
                    d.ContainsKey("transaction.id") &&
                    d.ContainsKey("customerId") && (int)d["customerId"] == 12345
                )
            ), Times.Once);
        }

        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.AutoDual)]
        public class MockableComObject
        {
            public string ToLogString() => "ID=555,Name=MockObject";
        }

        [Fact]
        public void SanitizeValue_HandlesAllDataTypesGracefully()
        {
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            var mockInternalLogger = new Mock<IInternalLogger>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, mockInternalLogger.Object);

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
}