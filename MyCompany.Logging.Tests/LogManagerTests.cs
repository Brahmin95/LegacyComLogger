using Moq;
using MyCompany.Logging.Abstractions;
using System;
using System.Reflection;
using Xunit;

// A unique namespace for our test-specific provider classes. This completely
// avoids any naming collisions with the real NLog provider.
namespace MyCompany.Logging.Tests.Internal.TestFailureProvider
{
    public class NLogInternalLogger : IInternalLogger
    {
        public static Mock<IInternalLogger> Mock { get; set; } = new Mock<IInternalLogger>();
        public void Trace(string m, Exception e = null) => Mock.Object.Trace(m, e);
        public void Debug(string m, Exception e = null) => Mock.Object.Debug(m, e);
        public void Info(string m, Exception e = null) => Mock.Object.Info(m, e);
        public void Warn(string m, Exception e = null) => Mock.Object.Warn(m, e);
        public void Error(string m, Exception e = null) => Mock.Object.Error(m, e);
        public void Fatal(string m, Exception e = null) => Mock.Object.Fatal(m, e);
    }
    public class NLogLoggerFactory : ILoggerFactory
    {
        public NLogLoggerFactory() => throw new InvalidOperationException("This factory is designed to fail for testing.");
        public ILogger GetLogger(string name) => throw new NotImplementedException();
    }
    public class ElasticApmTracer : ITracer
    {
        public void Trace(string name, TxType type, Action action) => action?.Invoke();
        public T Trace<T>(string name, TxType type, Func<T> func) => func.Invoke();
    }
    public static class NLogInitializer
    {
        public static void ConfigureDotNetContext() { }
        public static void ConfigureVb6Context() { }
    }
}

namespace MyCompany.Logging.Tests
{
    // By using a static using directive to our unique namespace, we can refer to "Mock" directly without ambiguity.
    using static MyCompany.Logging.Tests.Internal.TestFailureProvider.NLogInternalLogger;

    public class LogManagerTests : LoggingTestBase
    {
        [Fact]
        public void GetLogger_BeforeInitialize_ReturnsResilientNullLogger()
        {
            var logger = LogManager.GetLogger("Test");
            Assert.NotNull(logger);
            Assert.EndsWith("NullLogger", logger.GetType().Name);
        }

        [Fact]
        public void GetTracer_BeforeInitialize_ReturnsResilientNullTracer()
        {
            bool wasActionCalled = false;
            var tracer = LogManager.Tracer;
            tracer.Trace("Test", TxType.Process, () => { wasActionCalled = true; });
            Assert.NotNull(tracer);
            Assert.EndsWith("NullTracer", tracer.GetType().Name);
            Assert.True(wasActionCalled);
        }

        /// <summary>
        /// Verifies that calling Initialize with the real provider succeeds when prerequisites are met.
        /// </summary>
        [Fact]
        public void Initialize_WhenCalledOnce_SetsFactoryAndTracerAndIsInitialized()
        {
            // The real NLogLoggerFactory requires a configuration to exist.
            // We provide a minimal one for this test.
            NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();

            LogManager.Initialize(AppRuntime.DotNet);

            Assert.True(LogManager.IsInitialized);
            Assert.NotNull(LogManager.Tracer);
            Assert.IsType<MyCompany.Logging.NLogProvider.ElasticApmTracer>(LogManager.Tracer);
        }

        [Fact]
        public void Initialize_WhenCalledMultipleTimes_IsIdempotent()
        {
            var firstFactory = new Mock<ILoggerFactory>();
            var loggerFromFirstFactory = new Mock<ILogger>().Object;
            firstFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(loggerFromFirstFactory);
            InitializeWithMocks(firstFactory.Object, new Mock<IInternalLogger>().Object);
            LogManager.Tracer = new Mock<ITracer>().Object;

            LogManager.Initialize(AppRuntime.DotNet);

            var resultLogger = LogManager.GetLogger("Test");
            Assert.Same(loggerFromFirstFactory, resultLogger);
        }

        [Fact]
        public void GetCurrentClassLogger_ReturnsLoggerWithCorrectName()
        {
            var mockFactory = new Mock<ILoggerFactory>();
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);
            var logger = LogManager.GetCurrentClassLogger();
            mockFactory.Verify(f => f.GetLogger("MyCompany.Logging.Tests.LogManagerTests"), Times.Once);
        }

        /// <summary>
        /// Verifies that if initialization fails before the internal logger is created,
        /// the safety prompt is invoked.
        /// </summary>
        [Fact]
        public void Initialize_WithTotalFailure_InvokesSafetyPrompt()
        {
            bool wasPromptCalled = false;
            LogManager.SafetyOverridePrompt = (message) => { wasPromptCalled = true; return true; };

            LogManager.ProviderAssemblyName = "Invalid.Assembly.Name.That.Does.NotExist";

            LogManager.Initialize(AppRuntime.DotNet);

            Assert.True(wasPromptCalled, "The safety prompt delegate should have been called.");
            Assert.False(LogManager.IsInitialized);
        }

        /// <summary>
        /// Verifies that if initialization fails AFTER the internal logger is created,
        /// the failure is logged to the internal logger.
        /// </summary>
        [Fact]
        public void Initialize_WithPartialFailure_LogsToInternalLogger()
        {
            // Arrange
            Mock = new Mock<IInternalLogger>();
            LogManager.SafetyOverridePrompt = (message) => true;

            // Point LogManager to our test-only provider types.
            LogManager.ProviderAssemblyName = GetType().Assembly.GetName().Name;
            LogManager.InternalLoggerFullTypeName = "MyCompany.Logging.Tests.Internal.TestFailureProvider.NLogInternalLogger";
            LogManager.FactoryFullTypeName = "MyCompany.Logging.Tests.Internal.TestFailureProvider.NLogLoggerFactory";
            LogManager.TracerFullTypeName = "MyCompany.Logging.Tests.Internal.TestFailureProvider.ElasticApmTracer";
            LogManager.InitializerFullTypeName = "MyCompany.Logging.Tests.Internal.TestFailureProvider.NLogInitializer";

            // Act
            LogManager.Initialize(AppRuntime.DotNet);

            // Assert
            // THE FIX: The Verify expression now correctly expects a TargetInvocationException
            // and inspects its InnerException to find our original InvalidOperationException.
            Mock.Verify(
                log => log.Fatal(
                    It.Is<string>(s => s.Contains("Could not create or configure the main LoggerFactory or Tracer")),
                    It.Is<TargetInvocationException>(ex =>
                        ex.InnerException is InvalidOperationException &&
                        ex.InnerException.Message.Contains("This factory is designed to fail for testing.")
                    )
                ),
                Times.Once
            );

            Assert.False(LogManager.IsInitialized);
        }
    }
}