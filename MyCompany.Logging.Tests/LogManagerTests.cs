using Moq;
using MyCompany.Logging.Abstractions;
using Xunit;

namespace MyCompany.Logging.Tests
{
    public class LogManagerTests : LoggingTestBase
    {
        [Fact]
        public void GetLogger_BeforeInitialize_ReturnsResilientNullLogger()
        {
            // This test remains valid and unchanged.
            Assert.False(LogManager.IsInitialized);
            var logger = LogManager.GetLogger("Test");
            Assert.NotNull(logger);
            Assert.EndsWith("NullLogger", logger.GetType().Name);
        }

        [Fact]
        public void Initialize_WhenCalledOnce_SetsFactoryAndIsInitialized()
        {
            // ARRANGE & ACT
            // We now call the new, reflection-based Initialize method,
            // which is the public contract of the LogManager.
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);

            // ASSERT
            Assert.True(LogManager.IsInitialized);
        }

        [Fact]
        public void Initialize_WhenCalledMultipleTimes_IsIdempotent()
        {
            // ARRANGE
            // 1. We create mock objects for our first, "correct" initialization.
            var firstFactory = new Mock<ILoggerFactory>();
            var firstInternalLogger = new Mock<IInternalLogger>();
            var loggerFromFirstFactory = new Mock<ILogger>().Object;
            firstFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(loggerFromFirstFactory);

            // 2. We use our special test helper to set the state of LogManager with our mocks.
            // This simulates a successful first initialization.
            InitializeWithMocks(firstFactory.Object, firstInternalLogger.Object);

            // ACT
            // 3. We then attempt to call the real Initialize method again.
            //    The idempotent check inside Initialize should cause it to exit immediately.
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);

            // ASSERT
            // 4. We verify that the logger returned is still the one from our original
            //    mocked factory, proving that the second Initialize call did nothing.
            var resultLogger = LogManager.GetLogger("Test");
            Assert.Same(loggerFromFirstFactory, resultLogger);
        }
    }
}