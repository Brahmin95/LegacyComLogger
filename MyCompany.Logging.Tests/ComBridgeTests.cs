using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

// Add a using statement for the COM library we just referenced.
using Scripting;

namespace MyCompany.Logging.Tests
{
    public class ComBridgeTests : LoggingTestBase
    {
        [Fact]
        public void Info_WithScriptingDictionary_CorrectlyConvertsAndPassesProperties()
        {
            // ARRANGE
            var mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);

            var bridge = new LoggingComBridge();
            var vbProperties = new Dictionary();
            vbProperties.Add("aStringKey", "hello world");
            vbProperties.Add("anIntegerKey", 12345);
            vbProperties.Add("aBooleanKey", true);

            // ACT
            bridge.Info("Test.cls", "TestMethod", "Testing dictionary conversion", vbProperties);

            // ASSERT
            mockLogger.Verify(log => log.Info(
                "Testing dictionary conversion",
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["aStringKey"] == "hello world" &&
                    (int)d["anIntegerKey"] == 12345 &&
                    (bool)d["aBooleanKey"] == true &&
                    d.ContainsKey("vbCodeFile") &&
                    d.ContainsKey("vbMethodName")
                )
            ), Times.Once);
        }

        [Fact]
        public void Constructor_WhenLogManagerIsNotInitialized_SubsequentGetLoggerReturnsRealLogger()
        {
            Assert.False(LogManager.IsInitialized, "Precondition failed: LogManager should not be initialized.");
            var bridge = new LoggingComBridge();
            var resultLogger = LogManager.GetLogger("test");
            Assert.NotNull(resultLogger);
            Assert.DoesNotContain("NullLogger", resultLogger.GetType().Name);
        }

        [Fact]
        public void Constructor_WhenLogManagerIsAlreadyInitialized_DoesNotChangeExistingFactory()
        {
            // Arrange
            var mockFactory = new Mock<ILoggerFactory>();
            var mockLoggerInstance = new Mock<ILogger>().Object;
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLoggerInstance);
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);
            Assert.True(LogManager.IsInitialized, "Precondition failed: LogManager should be initialized with mocks.");

            // Act
            var bridge = new LoggingComBridge();
            var resultLogger = LogManager.GetLogger("test");

            // Assert
            // THIS IS THE CORRECTED LINE: The third "message" argument is removed.
            Assert.Same(mockLoggerInstance, resultLogger);
        }

        [Fact]
        public void CreatePropertiesWithTransactionId_ReturnsPopulatedDictionary()
        {
            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            Assert.NotNull(props.Item("transaction.id"));
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
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);

            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreateProperties();
            props.Add("goodComObject", new MockableComObject());

            bridge.Info("Test.cls", "TestMethod", "Data type test", props);

            mockLogger.Verify(log => log.Info(
                It.IsAny<string>(),
                It.Is<Dictionary<string, object>>(d => (string)d["goodComObject"] == "ID=555,Name=MockObject")
            ), Times.Once);
        }
    }
}