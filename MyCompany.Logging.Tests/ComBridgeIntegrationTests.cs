using MyCompany.Logging.ComBridge;
using System;
using System.Runtime.InteropServices;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// This is an INTEGRATION TEST, not a unit test.
    /// It verifies that the LoggingComBridge is correctly registered and callable via COM Interop.
    /// It uses low-level COM activation to ensure a true COM object is created and tested.
    /// 
    /// PREREQUISITE: For this test to pass, the MyCompany.Logging.ComBridge.dll must have been
    /// successfully registered with the system using `regasm.exe`. This is typically done
    /// in a post-build event or a setup script.
    /// Example Post-Build Command:
    /// "%Windir%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" "$(TargetPath)" /tlb /codebase
    /// </summary>
    public class ComBridgeIntegrationTests
    {
        #region COM P/Invoke Signatures and Interfaces

        private static readonly Guid IID_IClassFactory = new Guid("00000001-0000-0000-C000-000000000046");

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern void CoGetClassObject(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            uint dwClsContext,
            IntPtr pServerInfo,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
            void LockServer(bool fLock);
        }

        #endregion

        // We can remove the minimal test now, as it has served its diagnostic purpose.

        [Fact]
        [Trait("Category", "Integration")]
        public void CreateInstanceViaCom_AndCallMethod_Succeeds()
        {
            IClassFactory classFactory = null;
            object comBridgeObject = null; // Use a generic object to hold the reference

            try
            {
                // Arrange: Get the GUIDs for our COM class and interface.
                Guid clsid = new Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE"); // LoggingComBridge
                Guid iid = new Guid("A1B2C3D4-E5F6-4a7b-8c9d-0123456789AB");   // ILoggingComBridge

                // Act 1: Get the class factory. This proves registration is correct.
                object classFactoryObject;
                CoGetClassObject(clsid, 1, IntPtr.Zero, IID_IClassFactory, out classFactoryObject);
                classFactory = (IClassFactory)classFactoryObject;
                Assert.NotNull(classFactory);

                // Act 2: Use the factory to create the instance. This proves dependencies are found.
                classFactory.CreateInstance(null, ref iid, out comBridgeObject);
                Assert.NotNull(comBridgeObject);

                // Act 3: Cast to the interface to ensure the contract is correct.
                ILoggingComBridge comBridge = (ILoggingComBridge)comBridgeObject;

                // Act 4: Call a simple method to prove the object is alive and methods are callable.
                string transactionId = comBridge.CreateTransactionId();

                // Assert: The primary assertion is that the method call returned a valid result.
                // This proves the entire pipeline worked, from registration to activation to method invocation.
                Assert.False(string.IsNullOrEmpty(transactionId));
                Assert.Equal(32, transactionId.Length);
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
            {
                Assert.Fail($"The COM class is not registered. Please run `regasm.exe`. Details: {ex.Message}");
            }
            catch (Exception ex)
            {
                // If we get here, it's a genuine unexpected failure (e.g., constructor logic error).
                Assert.Fail($"The COM integration test failed with an unexpected error. Original Exception: {ex}");
            }
            finally
            {
                // The cleanup must be done carefully. We only call ReleaseComObject if the object
                // is *actually* a COM wrapper. Since we know it's not in the test runner,
                // we can make this check to prevent the error.
                if (comBridgeObject != null && Marshal.IsComObject(comBridgeObject))
                {
                    Marshal.ReleaseComObject(comBridgeObject);
                }
                if (classFactory != null && Marshal.IsComObject(classFactory))
                {
                    Marshal.ReleaseComObject(classFactory);
                }
            }
        }
    }
}