using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using NLog.Config;
using System;
using System.Collections.Generic;
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

        [Fact]
        public void NLogInitializer_ConfigureDotNetContext_SetsCorrectMdlc()
        {
            // ARRANGE
            var expectedCorrelationId = Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(TestCorrelationIdEnvVar, expectedCorrelationId);

            // ACT
            // We now test the initializer method directly, as it's a public static method.
            NLogInitializer.ConfigureDotNetContext();

            // ASSERT
            var actualCorrelationId = NLog.MappedDiagnosticsLogicalContext.Get("session.id");
            Assert.Equal(expectedCorrelationId, actualCorrelationId);
            Assert.Equal(".NET", NLog.MappedDiagnosticsLogicalContext.Get("labels.app_type"));
            Assert.NotNull(NLog.MappedDiagnosticsLogicalContext.Get("service.name"));
        }

        [Fact]
        public void Info_WithMessageTemplate_LogsCorrectEventProperties()
        {
            // ARRANGE
            // Initialize the LogManager using the correct new signature.
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);
            ILogger logger = LogManager.GetLogger("TestLogger");

            // ACT
            logger.Info("User {UserId} completed transaction {TransactionId}", 123, "txn-abc-456");

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal(123, logEvent.Properties["UserId"]);
            Assert.Equal("txn-abc-456", logEvent.Properties["TransactionId"]);
        }

        [Fact]
        public void Info_WithPropertyDictionary_LogsComplexKeyNamesCorrectly()
        {
            // ARRANGE
            LogManager.Initialize("MyCompany.Logging.NLogProvider", ApplicationEnvironment.DotNet);
            ILogger logger = LogManager.GetLogger("DictionaryTestLogger");

            var properties = new Dictionary<string, object>
            {
                { "user.id", "user-555" },
                { "transaction.id", "txn-xyz-789" }
            };

            // ACT
            logger.Info("Processing complex event", properties);

            // ASSERT
            Assert.Single(_testTarget.Events);
            var logEvent = _testTarget.Events[0];
            Assert.Equal("user-555", logEvent.Properties["user.id"]);
            Assert.Equal("txn-xyz-789", logEvent.Properties["transaction.id"]);
        }
    }
}