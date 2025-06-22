using System;

namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines a provider-agnostic contract for creating APM transactions.
    /// This is the primary interface for .NET developers to trace operations.
    /// </summary>
    public interface ITracer
    {
        /// <summary>
        /// Traces the execution of an action as a new APM transaction.
        /// </summary>
        /// <param name="name">The name of the transaction (e.g., "SaveCustomer").</param>
        /// <param name="type">The type of the transaction.</param>
        /// <param name="action">The action to be executed and traced.</param>
        void Trace(string name, TxType type, Action action);

        /// <summary>
        /// Traces the execution of a function as a new APM transaction.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="name">The name of the transaction (e.g., "GetCustomerById").</param>
        /// <param name="type">The type of the transaction.</param>
        /// <param name="func">The function to be executed and traced.</param>
        /// <returns>The result of the function.</returns>
        T Trace<T>(string name, TxType type, Func<T> func);
    }
}