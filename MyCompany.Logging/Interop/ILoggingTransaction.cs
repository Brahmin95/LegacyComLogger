using System; // Required for IDisposable
using System.Runtime.InteropServices;

namespace MyCompany.Logging.Interop
{
    /// <summary>
    /// Defines a COM-visible object that represents a single, scoped logging transaction or span.
    /// This object acts as a "handle". When it is released by the VB6 code (set to Nothing),
    /// its context is automatically and safely removed from the logging stack.
    /// In C# tests, it can be used with a `using` block.
    /// </summary>
    [ComVisible(true)]
    [Guid("B2C3D4E5-F6A7-4b8c-9d0e-1234567890BC")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ILoggingTransaction : IDisposable
    {
        // This interface is intentionally empty. Its purpose is to be a disposable "handle"
        // that VB6 can create and destroy to manage the scope's lifetime.
        // Implementing IDisposable makes it compatible with the C# `using` statement.
    }
}