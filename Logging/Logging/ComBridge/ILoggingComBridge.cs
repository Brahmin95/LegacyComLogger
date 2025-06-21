using System.Runtime.InteropServices;

namespace MyCompany.Logging.ComBridge
{
    /// <summary>
    /// Defines the COM-visible interface for the logging bridge, providing a strongly-typed
    /// contract for VB6 clients. This is the public API that VB6 developers will code against.
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
        /// Logs an Error message from a VB6 client.
        /// </summary>
        /// <param name="errorDetails">Optional. A string representation of the VB Err object or other error context.</param>
        void Error(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties);

        /// <summary>
        /// Logs a Fatal message from a VB6 client.
        /// </summary>
        void Fatal(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties);
    }
}