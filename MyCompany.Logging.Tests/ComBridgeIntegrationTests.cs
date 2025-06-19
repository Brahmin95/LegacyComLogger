using MyCompany.Logging.Abstractions;
using MyCompany.Logging.ComBridge;
using System;
using System.Runtime.InteropServices;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// This is an INTEGRATION TEST, not a unit test.
    /// It verifies that the LoggingComBridge is correctly registered and callable via COM Interop.
    /// 
    /// PREREQUISITE: For this test to pass, the MyCompany.Logging.ComBridge.dll must have been
    /// successfully registered with the system using `regasm.exe`. This is typically done
    /// in a post-build event or a setup script.
    /// Example Post-Build Command:
    /// "%Windir%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" "$(TargetPath)" /tlb /codebase
    /// </summary>
    public class ComBridgeIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")] // An xUnit trait to categorize this test
        public void CreateInstanceViaCom_AndCallMethod_Succeeds()
        {
            ILoggingComBridge comBridge = null;
            try
            {
                // Arrange: Get the Type from the registered ProgID, just like VB6 does.
                Type bridgeType = Type.GetTypeFromProgID("MyCompany.Logging.ComBridge");
                Assert.NotNull(bridgeType);

                // Act 1: Create an instance using COM. This will fail if not registered.
                comBridge = (ILoggingComBridge)Activator.CreateInstance(bridgeType);
                Assert.NotNull(comBridge);

                // Act 2: Call a simple method to ensure marshalling works.
                string transactionId = comBridge.CreateTransactionId();

                // Assert
                Assert.False(string.IsNullOrEmpty(transactionId));
                Assert.Equal(32, transactionId.Length); // Guid "N" format is 32 chars
            }
            catch (Exception ex)
            {
                // Provide a helpful failure message if the COM registration is missing.
                Assert.Fail($"The COM integration test failed. This is likely because the ComBridge DLL is not registered. Please run regasm.exe on MyCompany.Logging.ComBridge.dll. Original Exception: {ex.Message}");
            }
            finally
            {
                // Clean up the COM object.
                if (comBridge != null)
                {
                    Marshal.ReleaseComObject(comBridge);
                }
            }
        }
    }
}