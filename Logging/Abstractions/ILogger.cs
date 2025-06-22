using System;
using System.Collections.Generic;

namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines the primary, abstract contract for application-level logging.
    /// It provides two sets of overloads to create an idiomatic experience for both
    /// .NET and VB6 (via COM Bridge) consumers.
    /// </summary>
    public interface ILogger
    {
        // These overloads are for .NET consumers, using modern message templates and params arrays
        // for structured logging, which is familiar and efficient in C#.
        #region .NET Consumer Methods

        /// <summary>
        /// Writes a trace-level log event using a message template and arguments.
        /// </summary>
        /// <param name="messageTemplate">The message template (e.g., "Processing order {OrderId}").</param>
        /// <param name="args">The arguments to substitute into the template.</param>
        void Trace(string messageTemplate, params object[] args);

        /// <summary>
        /// Writes a debug-level log event using a message template and arguments.
        /// </summary>
        /// <param name="messageTemplate">The message template.</param>
        /// <param name="args">The arguments to substitute into the template.</param>
        void Debug(string messageTemplate, params object[] args);

        /// <summary>
        /// Writes an info-level log event using a message template and arguments.
        /// </summary>
        /// <param name="messageTemplate">The message template.</param>
        /// <param name="args">The arguments to substitute into the template.</param>
        void Info(string messageTemplate, params object[] args);

        /// <summary>
        /// Writes a warning-level log event using a message template and arguments.
        /// </summary>
        /// <param name="messageTemplate">The message template.</param>
        /// <param name="args">The arguments to substitute into the template.</param>
        void Warn(string messageTemplate, params object[] args);

        /// <summary>
        /// Writes an error-level log event with an exception, message template, and arguments.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="messageTemplate">The message template.</param>
        /// <param name="args">The arguments to substitute into the template.</param>
        void Error(Exception ex, string messageTemplate, params object[] args);

        /// <summary>
        /// Writes a fatal-level log event with an exception, message template, and arguments.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="messageTemplate">The message template.</param>
        /// <param name="args">The arguments to substitute into the template.</param>
        void Fatal(Exception ex, string messageTemplate, params object[] args);

        #endregion



        // These overloads are for the VB6 COM Bridge. They use a simple message string and
        // a Dictionary for properties, which is much easier and more robust to handle
        // across the COM interop boundary than params arrays.
        #region VB6 Consumer Methods

        /// <summary>
        /// Writes a trace-level log event with a simple message and optional properties.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="properties">A dictionary of key-value pairs for structured data.</param>
        void Trace(string message, Dictionary<string, object> properties = null);

        /// <summary>
        /// Writes a debug-level log event with a simple message and optional properties.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="properties">A dictionary of key-value pairs for structured data.</param>
        void Debug(string message, Dictionary<string, object> properties = null);

        /// <summary>
        /// Writes an info-level log event with a simple message and optional properties.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="properties">A dictionary of key-value pairs for structured data.</param>
        void Info(string message, Dictionary<string, object> properties = null);

        /// <summary>
        /// Writes a warning-level log event with a simple message and optional properties.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="properties">A dictionary of key-value pairs for structured data.</param>
        void Warn(string message, Dictionary<string, object> properties = null);

        /// <summary>
        /// Writes an error-level log event with a message, optional exception, and optional properties.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="ex">The optional exception to log.</param>
        /// <param name="properties">A dictionary of key-value pairs for structured data.</param>
        void Error(string message, Exception ex = null, Dictionary<string, object> properties = null);

        /// <summary>
        /// Writes a fatal-level log event with a message, optional exception, and optional properties.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="ex">The optional exception to log.</param>
        /// <param name="properties">A dictionary of key-value pairs for structured data.</param>
        void Fatal(string message, Exception ex = null, Dictionary<string, object> properties = null);

        #endregion
    }
}