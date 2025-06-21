using System.Collections.Generic;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// Abstracts the static Elastic.Apm.Agent to allow for dependency injection
    /// and unit testing of APM enrichment logic. This interface creates a "seam"
    /// that can be mocked in tests.
    /// </summary>
    public interface IApmAgentWrapper
    {
        /// <summary>
        /// Gets the ID of the current active transaction.
        /// </summary>
        /// <returns>The transaction ID, or null if no transaction is active.</returns>
        string GetCurrentTransactionId();

        /// <summary>
        /// Gets the ID of the current trace.
        /// </summary>
        /// <returns>The trace ID, or null if no transaction is active.</returns>
        string GetCurrentTraceId();

        /// <summary>
        /// Gets the ID of the current active span.
        /// </summary>
        /// <returns>The span ID, or null if no span is active or if the span is the transaction.</returns>
        string GetCurrentSpanId();

        /// <summary>
        /// Adds a dictionary of custom key-value pairs to the current active APM transaction.
        /// This context will be visible in the APM UI for captured transactions and errors.
        /// </summary>
        /// <param name="context">A dictionary of custom properties to add to the APM transaction.</param>
        void AddCustomContext(IDictionary<string, object> context);
    }
}