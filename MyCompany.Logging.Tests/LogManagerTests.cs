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
            Assert.False(LogManager.IsInitialized);
            var logger = LogManager.GetLogger("Test");
            Assert.NotNull(logger);
            Assert.Equal("NullLogger", logger.GetType().Name);
        }

        [Fact]
        public void Initialize_WhenCalledOnce_SetsFactoryAndIsInitialized()
        {
            var mockFactory = new Mock<ILoggerFactory>();
            LogManager.Initialize(mockFactory.Object);
            Assert.True(LogManager.IsInitialized);
        }

        [Fact]
        public void Initialize_WhenCalledMultipleTimes_IsIdempotent()
        {
            // This test will now pass because parallel execution is disabled,
            // so no other test will interfere with its static state.
            var firstFactory = new Mock<ILoggerFactory>();
            var loggerFromFirstFactory = new Mock<ILogger>().Object;
            firstFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(loggerFromFirstFactory);

            var secondFactory = new Mock<ILoggerFactory>().Object;

            LogManager.Initialize(firstFactory.Object);
            LogManager.Initialize(secondFactory);

            var resultLogger = LogManager.GetLogger("Test");
            Assert.Same(loggerFromFirstFactory, resultLogger);
        }
    }
}