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
        // NEW, BEHAVIOR-DRIVEN TESTS FOR THE CONSTRUCTOR
        // =======================================================

        [Fact]
        public void Constructor_WhenLogManagerIsNotInitialized_SubsequentGetLoggerReturnsRealLogger()
        {
            // Arrange
            Assert.False(LogManager.IsInitialized, "Precondition failed: LogManager should not be initialized.");

            // Act
            // The constructor should trigger the real initialization via reflection.
            var bridge = new LoggingComBridge();

            // Assert
            // We now ask the LogManager (which should be fully initialized) for a logger.
            // We can't check the type directly without referencing the provider, but we can
            // check that it is NOT our NullLogger, which proves initialization occurred.
            var resultLogger = LogManager.GetLogger("test");
            Assert.NotNull(resultLogger);
            Assert.DoesNotContain("NullLogger", resultLogger.GetType().Name);
        }

        [Fact]
        public void Constructor_WhenLogManagerIsAlreadyInitialized_DoesNotChangeExistingFactory()
        {
            // Arrange
            // 1. Create a mock factory that will serve as our "already initialized" state.
            var mockFactory = new Mock<ILoggerFactory>();
            var mockLoggerInstance = new Mock<ILogger>().Object;
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(mockLoggerInstance);

            // 2. Use our test helper to force LogManager into this initialized state.
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);
            Assert.True(LogManager.IsInitialized, "Precondition failed: LogManager should be initialized with mocks.");

            // Act
            // 3. Create the bridge. Its constructor's "if (!IsInitialized)" check should be false,
            //    so it should do nothing.
            var bridge = new LoggingComBridge();

            // Assert
            // 4. We ask the LogManager for a logger. If the constructor did nothing,
            //    the factory should still be our mock factory, and we should get back our mock logger.
            //    This assertion now correctly and clearly proves that the original factory was not replaced.
            var resultLogger = LogManager.GetLogger("test");
            Assert.Same(mockLoggerInstance, resultLogger);
        }


        // =======================================================
        // EXISTING TESTS (Unchanged and Still Valid)
        // =======================================================
        [Fact]
        public void CreatePropertiesWithTransactionId_ReturnsPopulatedDictionary()
        {
            var bridge = new LoggingComBridge();
            dynamic props = bridge.CreatePropertiesWithTransactionId();
            Assert.NotNull(props.Item("transaction.id"));
        }

        // ... all other tests from the last response remain correct ...
    }
}