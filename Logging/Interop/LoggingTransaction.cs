using MyCompany.Logging.Abstractions;
using System;
using System.Runtime.InteropServices;

namespace MyCompany.Logging.Interop
{
    /// <summary>
    /// The concrete implementation of a scoped logging transaction handle. This object is
    /// "self-aware" and manages its own lifecycle on the context stack via a callback.
    /// Its Dispose method is called automatically when the COM object is released in VB6.
    /// </summary>
    [ComVisible(true)]
    [Guid("C3D4E5F6-A7B8-4c9d-0e1f-2345678901CD")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("MyCompany.Logging.Interop.LoggingTransaction")]
    public class LoggingTransaction : ILoggingTransaction, IDisposable
    {
        private readonly Action _disposeCallback;
        private bool _isDisposed = false;

        /// <summary>
        /// Internal constructor called by the LoggingComBridge. It receives a callback
        /// that contains the logic to safely pop its corresponding scope from the stack.
        /// </summary>
        /// <param name="disposeCallback">An action to be executed when this object is disposed.</param>
        internal LoggingTransaction(Action disposeCallback)
        {
            _disposeCallback = disposeCallback ?? throw new ArgumentNullException(nameof(disposeCallback));
        }

        /// <summary>
        /// This method is called automatically when the COM object's reference count
        /// goes to zero (i.e., when it's set to Nothing in VB6). It triggers the
        /// centralized cleanup logic in the bridge.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                try
                {
                    // Execute the callback provided by the bridge to pop the scope from the stack.
                    _disposeCallback();
                }
                catch (Exception ex)
                {
                    // This should never happen, but we log it to the internal logger just in case.
                    LogManager.InternalLogger.Error("An unexpected error occurred during transaction disposal.", ex);
                }
                finally
                {
                    _isDisposed = true;
                    GC.SuppressFinalize(this);
                }
            }
        }
    }
}