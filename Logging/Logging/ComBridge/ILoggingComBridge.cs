using System.Runtime.InteropServices;

namespace MyCompany.Logging.ComBridge
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
        /// Creates a new, unique transaction ID string (a GUID without dashes).
        /// </summary>
        /// <returns>A new transaction ID string.</returns>
        string CreateTransactionId();

        /// <summary>
        /// Creates a new Scripting.Dictionary object for holding custom log properties.
        /// </summary>
        /// <returns>A COM object that can be used as a dictionary.</returns>
        object CreateProperties();

        /// <summary>
        /// Creates a new Scripting.Dictionary and pre-populates it with a new transaction ID.
        /// </summary>
        /// <returns>A COM dictionary containing a 'transaction.id' key.</returns>
        object CreatePropertiesWithTransactionId();

        /// <summary>
        /// Logs a Trace message from a VB6 client.
        /// </summary>
        /// <param name="codeFile">The name of the VB6 file (e.g., "frmMain.frm").</param>
        /// <param name="methodName">The name of the VB6 method or sub (e.g., "cmdSave_Click").</param>
        /// <param name="message">The log message.</param>
        /// <param name="properties">Optional. A Scripting.Dictionary of custom properties.</param>
        void Trace(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a Debug message from a VB6 client.
        /// </summary>
        void Debug(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs an Info message from a VB6 client.
        /// </summary>
        void Info(string codeFile, string methodName, string message, [Optional] object properties);

        /// <summary>
        /// Logs a Warn message from a VB6 client.
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
        /// specifically designed to capture rich diagnostic data from the VB6 Err object.
        /// </summary>
        /// <param name="codeFile">The name of the VB6 file.</param>
        /// <param name="methodName">The name of the VB6 method where the error handler resides.</param>
        /// <param name="errorDescription">The detailed error from Err.Description.</param>
        /// <param name="errorNumber">The error code from Err.Number.</param>
        /// <param name="errorSource">The source from Err.Source.</param>
        /// <param name="lineNumber">The line number from the Erl function. Pass Erl here; the framework handles the case where it returns 0.</param>
        /// <param name="message">Optional. A user-friendly message describing the operation that failed. If omitted, the errorDescription will be used.</param>
        /// <param name="properties">Optional. A Scripting.Dictionary of custom properties.</param>
        void ErrorHandler(string codeFile, string methodName, string errorDescription, long errorNumber, string errorSource, int lineNumber, [Optional] string message, [Optional] object properties);
    }
}