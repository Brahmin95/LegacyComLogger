using MyCompany.Logging.ComBridge;
using System;
using System.Runtime.InteropServices;
using Xunit;

namespace MyCompany.Logging.Tests
{
    /// <summary>
    /// This is an INTEGRATION TEST, not a unit test.
    /// It verifies that the LoggingComBridge is correctly registered and callable via COM Interop.
    /// It uses low-level COM activation to ensure a true COM object is created and tested,
    /// mimicking how a VB6 application would consume it.
    /// 
    /// PREREQUISITE: For this test to pass, the MyCompany.Logging.ComBridge.dll must have been
    /// successfully registered with the system using `regasm.exe`. This is typically done
    /// in a post-build event or a setup script.
    /// </summary>
    public class ComBridgeIntegrationTests : LoggingTestBase
    {
        #region COM P/Invoke Signatures and Interfaces
        // The standard COM GUID for the IClassFactory interface.
        private static readonly Guid IID_IClassFactory = new Guid("00000001-0000-0000-C000-000000000046");

        /// <summary>
        /// A P/Invoke declaration for the core COM API function CoGetClassObject.
        /// This function retrieves a pointer to the class factory for a given CLSID.
        /// </summary>
        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern void CoGetClassObject(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            uint dwClsContext, // CLSCTX_INPROC_SERVER = 1
            IntPtr pServerInfo, // Must be null for local activation
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        /// <summary>
        /// A .NET definition of the native COM IClassFactory interface, which is used
        /// to create instances of a COM class.
        /// </summary>
        [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            // THIS IS THE FIX: The method definitions are restored.
            void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
            void LockServer(bool fLock);
        }
        #endregion

        /// <summary>
        /// Verifies that the LoggingComBridge can be successfully activated via the COM subsystem
        /// and that its methods are callable.
        /// </summary>
        [Fact]
        [Trait("Category", "Integration")]
        public void CreateInstanceViaCom_AndCallMethod_Succeeds()
        {
            IClassFactory classFactory = null;
            object comBridgeObject = null;
            ILoggingTransaction transaction = null;

            try
            {
                // Arrange: Get the GUIDs for our COM class and interface from their attributes.
                Guid clsid = new Guid("F9E8D7C6-B5A4-4b3c-2a1b-9876543210FE"); // LoggingComBridge
                Guid iid = new Guid("A1B2C3D4-E5F6-4a7b-8c9d-0123456789AB");   // ILoggingComBridge

                // Act 1: Get the class factory. This proves the class is correctly registered in the registry.
                object classFactoryObject;
                const uint CLSCTX_INPROC_SERVER = 1;
                CoGetClassObject(clsid, CLSCTX_INPROC_SERVER, IntPtr.Zero, IID_IClassFactory, out classFactoryObject);
                classFactory = (IClassFactory)classFactoryObject;
                Assert.NotNull(classFactory);

                // Act 2: Use the factory to create an instance. This proves dependencies are found.
                classFactory.CreateInstance(null, ref iid, out comBridgeObject);
                Assert.NotNull(comBridgeObject);

                // Act 3: Cast to the specific interface. This proves the object implements the expected contract.
                ILoggingComBridge comBridge = (ILoggingComBridge)comBridgeObject;

                // Act 4: Call a simple method to prove the object is alive and methods are callable.
                // BeginTrace is a perfect test case on the new API. It should return another COM object.
                transaction = comBridge.BeginTrace("IntegrationTest", "test");

                // Assert: The primary assertion is that the method call returned a valid result.
                // This proves the entire pipeline worked: registration -> activation -> instantiation -> method invocation.
                Assert.NotNull(transaction);
                Assert.IsAssignableFrom<ILoggingTransaction>(transaction);
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
            {
                Assert.Fail($"The COM class is not registered. Please run `regasm.exe` on the ComBridge DLL. Details: {ex.Message}");
            }
            catch (Exception ex)
            {
                Assert.Fail($"The COM integration test failed with an unexpected error. Original Exception: {ex}");
            }
            finally
            {
                // Meticulous COM cleanup is required to release all references.
                if (transaction is IDisposable d) d.Dispose();
                if (comBridgeObject != null && Marshal.IsComObject(comBridgeObject)) Marshal.ReleaseComObject(comBridgeObject);
                if (classFactory != null && Marshal.IsComObject(classFactory)) Marshal.ReleaseComObject(classFactory);
            }
        }
    }
}