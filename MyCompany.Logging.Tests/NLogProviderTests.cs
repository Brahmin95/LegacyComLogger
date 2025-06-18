using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using NLog.Config;
using System;
using Xunit;
using NLogManager = NLog.LogManager;

namespace MyCompany.Logging.Tests
{
    public class NLogProviderTests : LoggingTestBase
    {
        private readonly UnitTestTarget _testTarget;

        public NLogProviderTests()
        {
            var config = new LoggingConfiguration();
            _testTarget = new UnitTestTarget();
            config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, _testTarget);
            NLogManager.Configuration = config;
        }

        // THIS IS THE NEW, CORRECT TEST FOR THE INITIALIZER.
        [Fact]
        public void NLogLoggerFactory_Constructor_SetsMdlcFromEnvironmentVariable()
        {
            // Arrange
            var expectedCorrelationId = Guid.NewGuid().ToString("N");
            // Use the constant defined in our base class for consistency
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, expectedCorrelationId);

            // Act
            // The simple act of creating the factory triggers the initializer.
            var factory = new NLogLoggerFactory();

            // Assert
            // We check the static MDLC state directly, immediately after the action.
            // No logging call is needed. This completely eliminates the race condition.
            var actualCorrelationId = NLog.MappedDiagnosticsLogicalContext.Get("sessionCorrelationId");
            Assert.Equal(expectedCorrelationId, actualCorrelationId);
        }

        [Fact]
        public void Info_WithMessageTemplate_LogsCorrectEventProperties()
        {
            // Arrange
            var factory = new NLogLoggerFactory();
            Abstractions.LogManager.Initialize(factory);
            ILogger logger = Abstractions.LogManager.GetLogger("TestLogger");

            // Act
            logger.Info("User {UserId}", 123);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal(123, logEvent.Properties["UserId"]);
        }

        [Fact]
        public void Error_WithException_LogsExceptionObject()
        {
            // Arrange
            var factory = new NLogLoggerFactory();
            Abstractions.LogManager.Initialize(factory);
            ILogger logger = Abstractions.LogManager.GetLogger("ErrorTestLogger");
            var ex = new InvalidOperationException("Test exception message");

            // Act
            logger.Error(ex, "An error occurred");

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.NotNull(logEvent.Exception);
            Assert.IsType<InvalidOperationException>(logEvent.Exception);
        }
    }
}