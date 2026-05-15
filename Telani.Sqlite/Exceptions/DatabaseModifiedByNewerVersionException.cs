namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that the database was modified by a newer version of telani
/// and as such is not compatible with this version.
/// </summary>
public sealed class DatabaseModifiedByNewerVersionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseModifiedByNewerVersionException"/> class.
    /// </summary>
    public DatabaseModifiedByNewerVersionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseModifiedByNewerVersionException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The message for this exception</param>
    public DatabaseModifiedByNewerVersionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseModifiedByNewerVersionException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The message for this exception</param>
    /// <param name="innerException">An inner exception</param>
    public DatabaseModifiedByNewerVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
