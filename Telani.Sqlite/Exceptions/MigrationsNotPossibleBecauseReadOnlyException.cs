namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that it was not possible to execute migrations, because
/// the project file was opened in read-only mode or is on a read-only storage location.
/// </summary>
public sealed class MigrationsNotPossibleBecauseReadOnlyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationsNotPossibleBecauseReadOnlyException"/> class.
    /// </summary>
    public MigrationsNotPossibleBecauseReadOnlyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationsNotPossibleBecauseReadOnlyException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The error message</param>
    public MigrationsNotPossibleBecauseReadOnlyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationsNotPossibleBecauseReadOnlyException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">An inner exception</param>
    public MigrationsNotPossibleBecauseReadOnlyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
