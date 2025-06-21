using System;

namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines a contract for logging the internal state and errors
    /// of the logging framework's components themselves. This provides a separate,
    /// resilient channel for diagnostics, ensuring that if the main logging pipeline
    /// fails, the reason for the failure can still be recorded.
    /// </summary>
    public interface IInternalLogger
    {
        /// <summary>
        /// Writes a trace-level internal log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log.</param>
        void Trace(string message, Exception exception = null);

        /// <summary>
        /// Writes a debug-level internal log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log.</param>
        void Debug(string message, Exception exception = null);

        /// <summary>
        /// Writes an info-level internal log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log.</param>
        void Info(string message, Exception exception = null);

        /// <summary>
        /// Writes a warning-level internal log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log.</param>
        void Warn(string message, Exception exception = null);

        /// <summary>
        /// Writes an error-level internal log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log.</param>
        void Error(string message, Exception exception = null);

        /// <summary>
        /// Writes a fatal-level internal log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log.</param>
        void Fatal(string message, Exception exception = null);
    }
}