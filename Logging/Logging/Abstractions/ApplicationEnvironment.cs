namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines the type of application environment initializing the logger.
    /// This is used by the LogManager to select the correct context configuration
    /// strategy (e.g., how to determine the application's name).
    /// </summary>
    public enum ApplicationEnvironment
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