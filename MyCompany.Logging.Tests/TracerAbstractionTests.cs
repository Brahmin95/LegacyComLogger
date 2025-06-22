using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.NLogProvider;
using System;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// Contains unit tests for the new Tracer abstraction, verifying both the
    /// LogManager's public API and the behavior of the concrete provider implementation.
    /// </summary>
    public class TracerAbstractionTests : LoggingTestBase
    {
        /// <summary>
        /// Verifies that the public LogManager.Tracer property correctly delegates
        /// calls to the underlying provider's ITracer implementation.
        /// This tests the .NET developer-facing API.
        /// </summary>
        [Fact]
        public void LogManagerTracer_WhenCalled_InvokesProviderImplementation()
        {
            // Arrange
            // 1. Create a mock ITracer that we can verify.
            var mockTracer = new Mock<ITracer>();

            // 2. Inject the mock into the static LogManager.
            // This bypasses reflection and allows us to test the static property's behavior.
            LogManager.Tracer = mockTracer.Object;

            // Act
            // 3. Call the public API that a .NET developer would use.
            LogManager.Tracer.Trace("Test Transaction", TxType.Process, () => { /* do nothing */ });

            // Assert
            // 4. Verify that our mock's Trace method was called with the correct parameters.
            mockTracer.Verify(t => t.Trace(
                "Test Transaction",
                TxType.Process,
                It.IsAny<Action>()
            ), Times.Once);
        }

        /// <summary>
        /// Verifies that if the Elastic APM agent is not configured, the
        /// ElasticApmTracer still executes the user's code without throwing.
        /// </summary>
        [Fact]
        public void ElasticApmTracer_WhenAgentIsNotConfigured_ExecutesAction()
        {
            // Arrange
            var tracer = new ElasticApmTracer();
            bool wasActionCalled = false;
            Action testAction = () => { wasActionCalled = true; };

            // We cannot truly mock Agent.IsConfigured, so this test relies on the
            // fact that in a standard test runner, the agent is not configured.
            // This test protects against future changes that might throw an
            // exception instead of safely executing the action.

            // Act
            var exception = Record.Exception(() => tracer.Trace("Test", TxType.Process, testAction));

            // Assert
            Assert.Null(exception); // No exception should have been thrown
            Assert.True(wasActionCalled); // The user's code must have been executed
        }
    }
}