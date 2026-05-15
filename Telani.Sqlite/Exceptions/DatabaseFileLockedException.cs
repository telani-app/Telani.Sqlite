namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that it was not possible to lock the database file, because
/// the project because the database file is already open.
/// </summary>
public sealed class DatabaseFileLockedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseFileLockedException"/> class with a specified error message.
    /// </summary>
    /// <param name="msg">the message</param>
    public DatabaseFileLockedException(string msg)
        : base(msg)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseFileLockedException"/> class.
    /// </summary>
    public DatabaseFileLockedException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseFileLockedException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="msg">the message</param>
    /// <param name="ex">an inner exception</param>
    public DatabaseFileLockedException(string msg, Exception ex)
        : base(msg, ex)
    {
    }
}
