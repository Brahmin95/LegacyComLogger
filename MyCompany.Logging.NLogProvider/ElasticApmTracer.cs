using MyCompany.Logging.Abstractions;
using System;
using Elastic.Apm; // This is the single, isolated dependency on the APM agent.

namespace MyCompany.Logging.NLogProvider
{
    /// <summary>
    /// The concrete implementation of ITracer that uses the Elastic APM Agent.
    /// This is the only class in the solution with a direct dependency on Elastic.Apm.
    /// </summary>
    public class ElasticApmTracer : ITracer
    {
        /// <inheritdoc/>
        public void Trace(string name, TxType type, Action action)
        {
            try
            {
                if (Agent.IsConfigured)
                {
                    Agent.Tracer.CaptureTransaction(name, type.ToString(), action);
                }
                else
                {
                    // If the agent isn't configured, we still MUST execute the user's code.
                    action();
                }
            }
            catch (Exception ex)
            {
                // Ensure that a failure in the APM agent does not crash the application.
                LogManager.InternalLogger.Error("ElasticApmTracer failed to capture transaction.", ex);
                // We MUST still execute the user's code even if tracing fails.
                action();
            }
        }

        /// <inheritdoc/>
        public T Trace<T>(string name, TxType type, Func<T> func)
        {
            try
            {
                if (Agent.IsConfigured)
                {
                    return Agent.Tracer.CaptureTransaction(name, type.ToString(), func);
                }
                else
                {
                    // If the agent isn't configured, we still MUST execute the user's code.
                    return func();
                }
            }
            catch (Exception ex)
            {
                // Ensure that a failure in the APM agent does not crash the application.
                LogManager.InternalLogger.Error("ElasticApmTracer failed to capture transaction.", ex);
                // We MUST still execute the user's code and return its result.
                return func();
            }
        }
    }
}