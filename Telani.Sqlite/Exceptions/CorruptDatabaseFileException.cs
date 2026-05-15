namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that the database file has failed integrity checks. This means the file is corrupt.
/// This corruption might be recoverable or not.
/// </summary>
public sealed class CorruptDatabaseFileException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorruptDatabaseFileException"/> class.
    /// </summary>
    public CorruptDatabaseFileException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CorruptDatabaseFileException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The exception's message</param>
    public CorruptDatabaseFileException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CorruptDatabaseFileException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The exception's message</param>
    /// <param name="innerException">An inner exception</param>
    public CorruptDatabaseFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}