using MyCompany.Logging.Abstractions;
using System.Runtime.InteropServices;

namespace MyCompany.Logging.Interop
{
    /// <summary>
    /// Defines the COM-visible interface for the logging bridge. This is the public API
    /// that VB6 developers will code against. It provides simple methods for standard logging
    /// and a special-purpose method for handling runtime errors.
    /// </summary>
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-4a7b-8c9d-0123456789AB")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ILoggingComBridge
    {
        /// <summary>
        /// Creates a new Scripting.Dictionary object for holding custom log properties.
        /// </summary>
        /// <returns>A COM object that can be used as a dictionary.</returns>
        object CreateProperties();

        /// <summary>
        /// Begins a new top-level trace and transaction. This is the entry point for a new unit of work.
        /// The returned object MUST be set to Nothing when the work is complete to ensure context is cleaned up.
        /// </summary>
        /// <param name="transactionName">A descriptive name for the transaction (e.g., "SaveCustomerClick").</param>
        /// <param name="transactionType">The type of transaction from the `TxType` enum.</param>
        /// <returns>A transaction handle object that must be released.</returns>
        ILoggingTransaction BeginTrace(string transactionName, TxType transactionType);

        /// <summary>
        /// Begins a new child span within the currently active trace. Use this to measure sub-operations.
        /// The returned object MUST be set to Nothing when the sub-operation is complete.
        /// </summary>
        /// <param name="spanName">A descriptive name for the span (e.g., "ValidateAddress").</param>
        /// <param name="spanType">The type of span from the `TxType` enum.</param>
        /// <returns>A span handle object that must be released.</returns>
        ILoggingTransaction BeginSpan(string spanName, TxType spanType);

        /// <summary>
        /// Logs a Trace message. Ambient context (trace.id, etc.) will be added automatically if active.
        /// </summary>
        /// <param name="codeFile">The name of the VB6 file (e.g., "frmMain.frm").</param>
        /// <param name="methodName">The name of the VB6 method or sub (e.g., "cmdSave_Click").</param>
        /// <param name="message">The log message.</param>
        /// <param name="properties">Optional. A Scripting.Dictionary of custom properties.</param>
        void Trace(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a Debug message. Ambient context will be added automatically if active.
        /// </summary>
        void Debug(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs an Info message. Ambient context will be added automatically if active.
        /// </summary>
        void Info(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a Warn message. Ambient context will be added automatically if active.
        /// </summary>
        void Warn(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a standard Error message. Use this for logical errors or known failure conditions
        /// where you do not have a VB6 Err object.
        /// </summary>
        /// <param name="codeFile">The name of the VB6 file (e.g., "frmMain.frm").</param>
        /// <param name="methodName">The name of the VB6 method or sub (e.g., "cmdSave_Click").</param>
        /// <param name="message">A descriptive message explaining the error.</param>
        /// <param name="properties">Optional. A Scripting.Dictionary of custom properties.</param>
        void Error(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a standard Fatal message. Use this for critical logical errors.
        /// </summary>
        /// <param name="codeFile">The name of the VB6 file.</param>
        /// <param name="methodName">The name of the VB6 method.</param>
        /// <param name="message">A descriptive message explaining the error.</param>
        /// <param name="properties">Optional. A Scripting.Dictionary of custom properties.</param>
        void Fatal(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a structured error from within a VB6 "On Error GoTo" block. This method is
        // specifically designed to capture rich diagnostic data from the VB6 Err object.
        /// The active trace/transaction/span context will be added automatically.
        /// </summary>
        /// <param name="codeFile">The name of the VB6 file.</param>
        /// <param name="methodName">The name of the VB6 method where the error handler resides.</param>
        /// <param name="message">A user-friendly message describing the operation that failed.</param>
        /// <param name="errorDescription">The detailed error from Err.Description.</param>
        /// <param name="errorNumber">The error code from Err.Number.</param>
        /// <param name="errorSource">The source from Err.Source.</param>
        /// <param name="lineNumber">The line number from the Erl function. Pass Erl here; the framework handles the case where it returns 0.</param>
        /// <param name="properties">Optional. A Scripting.Dictionary of custom properties.</param>
        void ErrorHandler(string codeFile, string methodName, string message, string errorDescription, long errorNumber, string errorSource, int lineNumber, [Optional] object properties);
    }
}