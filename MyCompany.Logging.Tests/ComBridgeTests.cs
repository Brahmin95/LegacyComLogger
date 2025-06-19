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
        // =======================================================
        // NEW TESTS FOR THE CONSTRUCTOR
        // =======================================================

        [Fact]
        public void Constructor_WhenLogManagerIsNotInitialized_InitializesLogManager()
        {
            // Arrange
            Assert.False(LogManager.IsInitialized, "Precondition failed: LogManager should not be initialized.");

            // Act
            // The constructor should trigger the reflection-based initialization.
            var bridge = new LoggingComBridge();

            // Assert
            // We verify that the LogManager is now in an initialized state.
            Assert.True(LogManager.IsInitialized, "The bridge constructor should have initialized the LogManager.");
        }

        [Fact]
        public void Constructor_WhenLogManagerIsAlreadyInitialized_DoesNothing()
        {
            // Arrange
            // 1. We create mock objects to represent the already-initialized state.
            var mockFactory = new Mock<ILoggerFactory>();
            var mockInternalLogger = new Mock<IInternalLogger>();
            var initialLogger = new Mock<ILogger>().Object;
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(initialLogger);

            // 2. We use our test helper to force LogManager into an initialized state with our mocks.
            InitializeWithMocks(mockFactory.Object, mockInternalLogger.Object);
            Assert.True(LogManager.IsInitialized, "Precondition failed: LogManager should be initialized with mocks.");

            // Act
            // 3. We create the bridge. Its constructor's "if (!IsInitialized)" check should be false.
            var bridge = new LoggingComBridge();

            // Assert
            // 4. We ask the LogManager for a logger. If the constructor did nothing,
            //    we should get back the original mocked logger instance, not a new real one.
            var resultLogger = LogManager.GetLogger("test");
            Assert.Same(initialLogger, resultLogger);
        }


        // =======================================================
        // EXISTING TESTS (Unchanged)
        // =======================================================

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
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);

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
        public class MockableComObject { public string ToLogString() => "ID=555,Name=MockObject"; }

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