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
        #region COM P/Invoke Helpers

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

        [Fact]
        [Trait("Category", "Integration")]
        public void CreateInstanceViaCom_AndCallMethod_Succeeds()
        {
            // Declare variables for all COM objects we will create.
            IClassFactory classFactory = null;
            ILoggingComBridge comBridge = null;

            try
            {
                // Arrange: Get the GUIDs for our COM class and interface.
                Guid clsid = new Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE"); // LoggingComBridge
                Guid iid = new Guid("A1B2C3D4-E5F6-4a7b-8c9d-0123456789AB");   // ILoggingComBridge

                // Act 1: Get the class factory for our COM object.
                object classFactoryObject;
                CoGetClassObject(clsid, 1, IntPtr.Zero, IID_IClassFactory, out classFactoryObject);
                classFactory = (IClassFactory)classFactoryObject;
                Assert.NotNull(classFactory);

                // Act 2: Use the factory to create a true COM instance.
                object comInstanceObject;
                classFactory.CreateInstance(null, ref iid, out comInstanceObject);
                Assert.NotNull(comInstanceObject);

                // Act 3: Cast the raw COM object to our .NET interface to make it usable.
                comBridge = (ILoggingComBridge)comInstanceObject;

                // Act 4: Call a simple method to ensure marshalling works.
                string transactionId = comBridge.CreateTransactionId();

                // Assert
                Assert.False(string.IsNullOrEmpty(transactionId));
                Assert.Equal(32, transactionId.Length);
                Assert.True(Marshal.IsComObject(comBridge));
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
            {
                // This is a specific, expected failure if regasm hasn't been run.
                // We fail the test with a helpful message.
                Assert.True(false, $"The COM class is not registered. Please run `regasm.exe` on MyCompany.Logging.ComBridge.dll before running this integration test. Details: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch any other unexpected exceptions.
                Assert.True(false, $"The COM integration test failed with an unexpected error: {ex.Message}");
            }
            finally
            {
                // =======================================================
                // THE CORRECTED CLEANUP LOGIC
                // =======================================================
                // We must release both COM objects we created, in reverse
                // order of creation.

                // 1. Release the main component instance.
                if (comBridge != null && Marshal.IsComObject(comBridge))
                {
                    Marshal.ReleaseComObject(comBridge);
                }

                // 2. Release the class factory instance.
                if (classFactory != null && Marshal.IsComObject(classFactory))
                {
                    Marshal.ReleaseComObject(classFactory);
                }
            }
        }
    }
}