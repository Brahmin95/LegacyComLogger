namespace MyCompany.Logging.Abstractions
{
    /// <summary>
    /// Defines the type of application environment initializing the logger,
    /// replacing magic strings for type safety.
    /// </summary>
    public enum ApplicationEnvironment
    {
        DotNet,
        Vb6
    }
}