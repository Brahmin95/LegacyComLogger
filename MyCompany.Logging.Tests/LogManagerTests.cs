using Moq;
using MyCompany.Logging.Abstractions;
using System;
using System.IO;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// Contains unit tests for the static MyCompany.Logging.Abstractions.LogManager class.
    /// These tests verify the initialization logic, idempotency, and resilience of the framework's entry point.
    /// </summary>
    public class LogManagerTests : LoggingTestBase
    {
        /// <summary>
        /// Verifies that calling GetLogger before the framework is initialized returns a non-null
        /// "NullLogger" instance, preventing runtime NullReferenceExceptions.
        /// </summary>
        [Fact]
        public void GetLogger_BeforeInitialize_ReturnsResilientNullLogger()
        {
            // Arrange
            // The LoggingTestBase ensures LogManager is not initialized.
            Assert.False(LogManager.IsInitialized);

            // Act
            var logger = LogManager.GetLogger("Test");

            // Assert
            Assert.NotNull(logger);
            Assert.EndsWith("NullLogger", logger.GetType().Name);
        }

        /// <summary>
        /// Verifies that calling GetTracer before the framework is initialized returns a non-null
        /// "NullTracer" instance that safely executes the wrapped code.
        /// </summary>
        [Fact]
        public void GetTracer_BeforeInitialize_ReturnsResilientNullTracer()
        {
            // Arrange
            Assert.False(LogManager.IsInitialized);
            bool wasActionCalled = false;

            // Act
            var tracer = LogManager.Tracer;
            tracer.Trace("Test", TxType.Process, () => { wasActionCalled = true; });

            // Assert
            Assert.NotNull(tracer);
            Assert.EndsWith("NullTracer", tracer.GetType().Name);
            Assert.True(wasActionCalled); // Verify the NullTracer still executes the code.
        }

        /// <summary>
        /// Verifies that calling Initialize with a valid provider assembly name sets the
        /// IsInitialized flag and correctly instantiates both the factory and the tracer.
        /// </summary>
        [Fact]
        public void Initialize_WhenCalledOnce_SetsFactoryAndTracerAndIsInitialized()
        {
            // Arrange & Act
            // We call the real Initialize method, which uses reflection to load the provider.
            // This relies on the NLogProvider project being referenced by the test project.
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);

            // Assert
            Assert.True(LogManager.IsInitialized);
            Assert.NotNull(LogManager.Tracer);
            Assert.EndsWith("ElasticApmTracer", LogManager.Tracer.GetType().Name);
        }

        /// <summary>
        /// Verifies that calling Initialize multiple times does not re-initialize the framework,
        /// proving that the initialization logic is safely idempotent.
        /// </summary>
        [Fact]
        public void Initialize_WhenCalledMultipleTimes_IsIdempotent()
        {
            // Arrange
            // Simulate a successful first initialization by setting the internal factory.
            var firstFactory = new Mock<ILoggerFactory>();
            var loggerFromFirstFactory = new Mock<ILogger>().Object;
            firstFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(loggerFromFirstFactory);
            InitializeWithMocks(firstFactory.Object, new Mock<IInternalLogger>().Object);
            LogManager.Tracer = new Mock<ITracer>().Object; // Also set a mock tracer

            // Act
            // Attempt to call the real Initialize method again.
            // The idempotent check `if (IsInitialized) return;` should cause it to exit immediately.
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);

            // Assert
            // Verify that the logger returned is still the one from our original mock factory,
            // proving that the second Initialize call did nothing.
            var resultLogger = LogManager.GetLogger("Test");
            Assert.Same(loggerFromFirstFactory, resultLogger);
        }

        /// <summary>
        /// Verifies that the GetCurrentClassLogger helper method correctly resolves the
        /// full name of the class from which it is called.
        /// </summary>
        [Fact]
        public void GetCurrentClassLogger_ReturnsLoggerWithCorrectName()
        {
            // Arrange
            var mockFactory = new Mock<ILoggerFactory>();
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);

            // Act
            // The call to GetCurrentClassLogger is made from within this test method,
            // which is part of the LogManagerTests class.
            var logger = LogManager.GetCurrentClassLogger();

            // Assert
            // We expect the logger name to be the full name of this test class.
            mockFactory.Verify(f => f.GetLogger("MyCompany.Logging.Tests.LogManagerTests"), Times.Once);
        }

        /// <summary>
        /// Verifies that if initialization fails due to a missing assembly, the safety prompt delegate
        /// is invoked and the framework remains uninitialized.
        /// </summary>
        [Fact]
        public void Initialize_WithInvalidProvider_InvokesSafetyPromptAndDoesNotInitialize()
        {
            // Arrange
            bool wasPromptCalled = false;
            string capturedMessage = null;

            // Override the production UI dialog with a mock delegate for this test.
            // We simulate the user choosing "Yes, proceed" to prevent Environment.Exit(1).
            LogManager.SafetyOverridePrompt = (message) =>
            {
                wasPromptCalled = true;
                capturedMessage = message;
                return true; // Simulate user clicking "Yes" to proceed.
            };

            // Act
            // We call initialize with a deliberately invalid assembly name that will cause
            // Assembly.Load to throw a FileNotFoundException, triggering the failure logic.
            LogManager.Initialize("Invalid.Assembly.Name.That.Does.NotExist", ApplicationEnvironment.DotNet);

            // Assert
            Assert.True(wasPromptCalled, "The safety prompt delegate should have been called.");
            Assert.Contains("CRITICAL", capturedMessage);
            Assert.Contains("could not be initialized", capturedMessage);
            Assert.False(LogManager.IsInitialized, "The LogManager should not be in an initialized state after a critical failure.");
        }
    }
}