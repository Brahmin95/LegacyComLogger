using Elastic.Apm;
using System.Collections.Generic;

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// The production implementation of IApmAgentWrapper that calls the
    /// real, static Elastic APM Agent. The static dependency is now safely
    /// isolated within this single class.
    /// </summary>
    public class ElasticApmAgentWrapper : IApmAgentWrapper
    {
        /// <inheritdoc/>
        public string GetCurrentTransactionId()
        {
            return Agent.Tracer.CurrentTransaction?.Id;
        }

        /// <inheritdoc/>
        public string GetCurrentTraceId()
        {
            return Agent.Tracer.CurrentTransaction?.TraceId;
        }

        /// <inheritdoc/>
        public string GetCurrentSpanId()
        {
            var currentTransaction = Agent.Tracer.CurrentTransaction;
            var currentSpan = Agent.Tracer.CurrentSpan;

            // Per Elastic Common Schema, a span.id should only be present if it's
            // different from the transaction.id (i.e., it's a child span).
            if (currentTransaction != null && currentSpan != null && currentSpan.Id != currentTransaction.Id)
            {
                return currentSpan.Id;
            }

            return null;
        }

        /// <inheritdoc/>
        public void AddCustomContext(IDictionary<string, object> context)
        {
            // Get the currently active transaction. If there isn't one, do nothing.
            var transaction = Agent.Tracer.CurrentTransaction;
            if (transaction == null || context == null)
            {
                return;
            }

            // The correct method is ITransaction.SetLabel(). We must iterate through the
            // dictionary and add each item as a label on the transaction.
            foreach (var entry in context)
            {
                // The SetLabel method has overloads for string, bool, and number types.
                // We use a switch with type patterns to call the correct overload and
                // provide a safe fallback for any other complex types.
                switch (entry.Value)
                {
                    case string s:
                        transaction.SetLabel(entry.Key, s);
                        break;
                    case bool b:
                        transaction.SetLabel(entry.Key, b);
                        break;
                    case int i:
                        transaction.SetLabel(entry.Key, i);
                        break;
                    case long l:
                        transaction.SetLabel(entry.Key, l);
                        break;
                    case double d:
                        transaction.SetLabel(entry.Key, d);
                        break;
                    case decimal m:
                        // SetLabel doesn't have a decimal overload, so we convert it to double.
                        transaction.SetLabel(entry.Key, (double)m);
                        break;
                    default:
                        // For any other type, we fall back to a string representation.
                        transaction.SetLabel(entry.Key, entry.Value?.ToString() ?? "null");
                        break;
                }
            }
        }
    }
}