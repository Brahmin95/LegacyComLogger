using Elastic.Apm;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// The production implementation of IApmAgentWrapper that calls the
    /// real, static Elastic APM Agent.
    /// </summary>
    public class ElasticApmAgentWrapper : IApmAgentWrapper
    {
        public string GetCurrentTransactionId()
        {
            // The static dependency is now safely isolated in this single class.
            return Agent.Tracer.CurrentTransaction?.Id;
        }

        public string GetCurrentTraceId()
        {
            return Agent.Tracer.CurrentTransaction?.TraceId;
        }

        public string GetCurrentSpanId()
        {
            // Only return a span.id if it's different from the transaction.id
            var currentTransaction = Agent.Tracer.CurrentTransaction;
            var currentSpan = Agent.Tracer.CurrentSpan;

            if (currentTransaction != null && currentSpan != null && currentSpan.Id != currentTransaction.Id)
            {
                return currentSpan.Id;
            }

            return null;
        }
    }
}