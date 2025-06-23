using System;
using System.Runtime.InteropServices;

namespace myapp.logging.Interop // Use your actual namespace
{
    // A completely new, unique GUID for this test class
    [Guid("1EAD2E1D-3C8E-45A2-9C5B-2A0D8D6A478B")]
    [ComVisible(true)]
    [ProgId("myapp.logging.Interop.MinimalComTest")]
    [ClassInterface(ClassInterfaceType.AutoDual)] // Use AutoDual for simplicity in this test
    public class MinimalComTest
    {
        /// <summary>
        /// A parameterless constructor with ZERO logic inside.
        /// This is the simplest possible activation path.
        /// </summary>
        public MinimalComTest()
        {
        }

        /// <summary>
        /// A simple method that proves the object was created and can be called.
        /// </summary>
        /// <returns>A confirmation string.</returns>
        public string Ping()
        {
            return "Pong";
        }
    }
}