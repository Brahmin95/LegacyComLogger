namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// Abstracts the static Elastic.Apm.Agent to allow for dependency injection
    /// and unit testing of APM enrichment logic.
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
        /// <returns>The span ID, or null if no span is active.</returns>
        string GetCurrentSpanId();
    }
}