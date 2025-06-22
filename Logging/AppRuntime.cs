namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines the type of application runtime initializing the logger.
    /// This is used by the LogManager to select the correct context configuration
    /// strategy (e.g., how to determine the application's name).
    /// </summary>
    public enum AppRuntime
    {
        /// <summary>
        /// The logging framework is being used by a standard .NET application.
        /// </summary>
        DotNet,

        /// <summary>
        /// The logging framework is being used by a VB6 application via the COM Bridge.
        /// </summary>
        Vb6
    }
}