using System;
using System.Runtime.Serialization;

namespace MyCompany.Logging.ComBridge
{
    /// <summary>
    /// Represents a structured error that originated from the VB6 part of the application.
    /// It captures the state of the VB6 Err object at the time of a runtime crash,
    /// enabling powerful filtering and analysis in logging and APM systems.
    /// </summary>
    [Serializable]
    public class VBErrorException : Exception
    {
        /// <summary>
        /// Gets the original VB6 error number (from Err.Number).
        /// </summary>
        public long VbErrorNumber { get; }

        /// <summary>
        /// Gets the original VB6 error source (from Err.Source).
        /// </summary>
        public string VbErrorSource { get; }

        /// <summary>
        /// Gets the line number where the error occurred (from Erl), if available.
        /// </summary>
        public int? VbLineNumber { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VBErrorException"/> class with rich, structured error details from VB6.
        /// </summary>
        /// <param name="message">The descriptive message of the error (from Err.Description).</param>
        /// <param name="errorNumber">The value of Err.Number.</param>
        /// <param name="errorSource">The value of Err.Source.</param>
        /// <param name="lineNumber">The line number from the Erl function.</param>
        public VBErrorException(string message, long errorNumber, string errorSource, int? lineNumber)
            : base(message)
        {
            this.VbErrorNumber = errorNumber;
            this.VbErrorSource = errorSource;
            this.VbLineNumber = lineNumber;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VBErrorException"/> class for simple logical errors.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public VBErrorException(string message) : base(message)
        {
            // Set default values for non-structured errors.
            this.VbErrorNumber = -1;
            this.VbErrorSource = "LogicalError";
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="VBErrorException"/> class with serialized data.
        /// </summary>
        protected VBErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            VbErrorNumber = info.GetInt64("VbErrorNumber");
            VbErrorSource = info.GetString("VbErrorSource");
            VbLineNumber = (int?)info.GetValue("VbLineNumber", typeof(int?));
        }

        /// <summary>
        /// Sets the SerializationInfo with information about the exception.
        /// </summary>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("VbErrorNumber", VbErrorNumber);
            info.AddValue("VbErrorSource", VbErrorSource);
            info.AddValue("VbLineNumber", VbLineNumber, typeof(int?));
        }
    }
}