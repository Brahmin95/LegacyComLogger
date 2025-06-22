using Moq;
using MyCompany.Logging.Abstractions;
using MyCompany.Logging.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// Contains low-level unit tests for specific helper methods and conventions
    /// within the LoggingComBridge, such as COM object sanitization.
    /// Note: Tests for the ambient context scope are in ComBridgeScopeTests.cs.
    /// </summary>
    public class ComBridgeTests : LoggingTestBase
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly LoggingComBridge _bridge;

        public ComBridgeTests()
        {
            _mockLogger = new Mock<ILogger>();
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.GetLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
            InitializeWithMocks(mockFactory.Object, new Mock<IInternalLogger>().Object);

            _bridge = new LoggingComBridge();
        }

        /// <summary>
        /// A mock COM-visible class used to test the ToLogString convention.
        /// </summary>
        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.AutoDual)]
        public class MockableComObject
        {
            public string ToLogString() => "ID=555,Name=MockObject";
        }

        /// <summary>
        /// Verifies that the bridge's internal SanitizeValue method correctly calls the
        /// ToLogString() method on a COM object if it exists, following the established convention.
        /// </summary>
        [Fact]
        public void ErrorHandler_WithComplexComProperty_CallsToLogString()
        {
            // Arrange
            // Create a dictionary of properties that includes our mock COM object.
            dynamic props = _bridge.CreateProperties();
            props.Add("complexObject", new MockableComObject());

            // Act
            // Call the ErrorHandler method, passing the properties.
            _bridge.ErrorHandler("Test.cls", "TestMethod", "Error with complex data", "Err.Description", 123, "Err.Source", 456, props);

            // Assert
            // Verify that the properties dictionary passed to the final logger contains the sanitized string.
            _mockLogger.Verify(log => log.Error(
                "Error with complex data",
                It.IsAny<VBErrorException>(),
                It.Is<Dictionary<string, object>>(d =>
                    (string)d["complexObject"] == "ID=555,Name=MockObject"
                )
            ), Times.Once);
        }
    }
}