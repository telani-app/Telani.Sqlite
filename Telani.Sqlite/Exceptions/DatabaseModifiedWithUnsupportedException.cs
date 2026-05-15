namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that the database was created by an unknown 3rd party application
/// and as such might not be compatible with this app.
/// </summary>
public sealed class DatabaseModifiedWithUnsupportedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseModifiedWithUnsupportedException"/> class.
    /// </summary>
    public DatabaseModifiedWithUnsupportedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseModifiedWithUnsupportedException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The message for this exception</param>
    public DatabaseModifiedWithUnsupportedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseModifiedWithUnsupportedException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The message for this exception</param>
    /// <param name="innerException">An inner exception</param>
    public DatabaseModifiedWithUnsupportedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
