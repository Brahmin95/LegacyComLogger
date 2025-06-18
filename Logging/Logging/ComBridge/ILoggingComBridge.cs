using System.Runtime.InteropServices;

namespace MyCompany.Logging.ComBridge
{
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-4a7b-8c9d-0123456789AB")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ILoggingComBridge
    {
        string CreateTransactionId();
        object CreateProperties();
        object CreatePropertiesWithTransactionId();
        void Trace(string codeFile, string methodName, string message, [Optional] object properties);
        void Debug(string codeFile, string methodName, string message, [Optional] object properties);
        void Info(string codeFile, string methodName, string message, [Optional] object properties);
        void Warn(string codeFile, string methodName, string message, [Optional] object properties);
        void Error(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties);
        void Fatal(string codeFile, string methodName, string message, [Optional] string errorDetails, [Optional] object properties);
    }
}