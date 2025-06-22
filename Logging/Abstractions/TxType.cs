using System.Runtime.InteropServices;

namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines a strongly-typed set of transaction types tailored for the application domain.
    /// This ensures consistency in transaction naming and provides IntelliSense for developers.
    /// This enum is made COM-visible for consumption by VB6.
    /// </summary>
    [ComVisible(true)]
    public enum TxType
    {
        /// <summary>
        /// A transaction initiated by a direct user action, like a button click.
        /// </summary>
        UserInteraction,

        /// <summary>
        /// A transaction that encompasses the loading and rendering of a screen or form.
        /// </summary>
        ScreenLoad,

        /// <summary>
        /// A transaction specifically for querying or searching for data.
        /// </summary>
        DataSearch,

        /// <summary>
        /// A generic server-side or business logic process.
        /// </summary>
        Process,

        /// <summary>
        /// A long-running or asynchronous task that is not directly tied to a user interaction.
        /// </summary>
        BackgroundTask
    }
}