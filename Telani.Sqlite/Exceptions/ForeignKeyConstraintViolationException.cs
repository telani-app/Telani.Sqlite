namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that the database contains foreign key constraint violations.
/// </summary>
public sealed class ForeignKeyConstraintViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyConstraintViolationException"/> class.
    /// </summary>
    public ForeignKeyConstraintViolationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyConstraintViolationException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The message for the exception</param>
    public ForeignKeyConstraintViolationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForeignKeyConstraintViolationException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The message for the exception</param>
    /// <param name="innerException">The inner exception</param>
    public ForeignKeyConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
