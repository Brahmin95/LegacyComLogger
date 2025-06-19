using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using NLog.Config;
using System;
using Xunit;
using NLogManager = NLog.LogManager;

namespace MyCompany.Logging.Tests
{
    // This now inherits from the fixed base class, so it runs sequentially.
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

        [Fact]
        public void NLogLoggerFactory_Constructor_SetsMdlcFromEnvironmentVariable()
        {
            // Arrange
            var expectedCorrelationId = Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, expectedCorrelationId);

            // Act
            var factory = new NLogLoggerFactory(); // This triggers the initializer

            // Assert
            // Assert against the new ECS key 'session.id'
            var actualCorrelationId = NLog.MappedDiagnosticsLogicalContext.Get("session.id");
            Assert.Equal(expectedCorrelationId, actualCorrelationId);

            // Also assert the other context items set by ConfigureDotNetContext
            Assert.Equal(".NET", NLog.MappedDiagnosticsLogicalContext.Get("labels.app_type"));
            Assert.NotNull(NLog.MappedDiagnosticsLogicalContext.Get("service.name"));
        }

        [Fact]
        public void Info_WithMessageTemplate_LogsCorrectEventProperties()
        {
            // Arrange
            var factory = new NLogLoggerFactory();
            LogManager.Initialize(factory);
            ILogger logger = LogManager.GetLogger("TestLogger");
            var transactionId = "txn-abc-456";

            // Act
            // --- THE FIX ---
            // Use a simple, valid C#-like identifier for the placeholder.
            // Do not use dots or special characters inside the {}.
            logger.Info("User {UserId} completed transaction {TransactionId}", 123, transactionId);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];

            // The keys in the dictionary will now match the placeholder names exactly.
            Assert.Equal(123, logEvent.Properties["UserId"]);
            Assert.Equal(transactionId, logEvent.Properties["TransactionId"]); // NLog uses the name of the placeholder
        }

        [Fact]
        public void Info_WithPropertyDictionary_LogsComplexKeyNamesCorrectly()
        {
            // Arrange
            var factory = new NLogLoggerFactory();
            LogManager.Initialize(factory);
            // Get the logger using the dictionary-based overload
            ILogger logger = LogManager.GetLogger("DictionaryTestLogger");

            var properties = new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "user.id", "user-555" },
                                        { "transaction.id", "txn-xyz-789" }
                                    };

            // Act
            // Call the dictionary-based Info method
            logger.Info("Processing complex event", properties);

            // Assert
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];

            // Now we can assert that the keys with dots exist and have the correct values.
            Assert.Equal("user-555", logEvent.Properties["user.id"]);
            Assert.Equal("txn-xyz-789", logEvent.Properties["transaction.id"]);
        }

        [Fact]
        public void Error_WithException_LogsExceptionObject()
        {
            // Arrange
            var factory = new NLogLoggerFactory();
            LogManager.Initialize(factory);
            ILogger logger = LogManager.GetLogger("ErrorTestLogger");
            var ex = new InvalidOperationException("Test exception message");

            // Act
            logger.Error(ex, "An error occurred");

            // Assert
            // This test is unaffected by the property name changes and should pass
            // once the isolation issues in the base class are fixed.
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.NotNull(logEvent.Exception);
            Assert.IsType<InvalidOperationException>(logEvent.Exception);
        }
    }
}