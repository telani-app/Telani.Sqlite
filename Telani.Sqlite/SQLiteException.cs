namespace Telani.Sqlite;

#pragma warning disable CA1707 // Identifiers should not contain underscores
/// <summary>
/// An easy to understand category for <see cref="SQLiteException"/>.
/// </summary>
public enum SQLiteExceptionSubType
{
    /// <summary>
    /// The default value, cause of exception could be anything.
    /// </summary>
    SQLITE_GENERAL,

    /// <summary>
    /// The database file is corrupt. :-(
    /// </summary>
    SQLITE_CORRUPT,

    /// <summary>
    /// A constraint violation occurred. This is almost certainly a bug in our code.
    /// </summary>
    SQLITE_CONSTRAINT,
}
#pragma warning restore CA1707 // Identifiers should not contain underscores

/// <summary>
/// An exception to signal that an exception or error has occurred when transacting with the database.
/// This is mostly used to wrap SQLite error codes.
/// </summary>
public sealed class SQLiteException : Exception
{
    /// <summary>
    /// The <see cref="SQLiteExceptionSubType"/> the narrows down the cause of this exception.
    ///
    /// This can be seen as a bit of pre-parsing of error code and extended error code,
    /// so enable simpler interpretation.
    /// </summary>
    public SQLiteExceptionSubType SubType { get; }

    /// <summary>
    /// The extended error code that SQLite returned.
    /// <seealso href="https://www.sqlite.org/rescode.html#primary_result_code_list"/>
    /// </summary>
    public int SqlLiteErrorCode { get; set; }

    /// <summary>
    /// The extended error code that SQLite returned.
    /// <seealso href="https://www.sqlite.org/rescode.html#extended_result_code_list"/>
    /// </summary>
    public int ExtendedErrorCode { get; set; }

    /// <summary>
    /// The string command that lead to this exception. Only sometimes provided.
    /// </summary>
    public string? CommandText { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteException"/> class of a specific <see cref="SubType"/>.
    /// </summary>
    /// <param name="subtype">The sub-type of exception this is.</param>
    public SQLiteException(SQLiteExceptionSubType subtype)
    {
        SubType = subtype;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteException"/> class.
    /// </summary>
    public SQLiteException()
    {
        SubType = SQLiteExceptionSubType.SQLITE_GENERAL;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteException"/> class of a specific <see cref="SubType"/> with a message.
    /// </summary>
    /// <param name="subtype">The sub-type of exception this is.</param>
    /// <param name="message">The message</param>
    public SQLiteException(SQLiteExceptionSubType subtype, string message)
        : base(message)
    {
        SubType = subtype;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="innerException">With an inner exception</param>
    public SQLiteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message</param>
    public SQLiteException(string message)
        : base(message)
    {
        SubType = SQLiteExceptionSubType.SQLITE_GENERAL;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return base.ToString() + "(SubType: " + SubType.ToString() + ")";
    }
}
