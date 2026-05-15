namespace Telani.Sqlite.Exceptions;

/// <summary>
/// An exception to signal that the database contains features that are not supported by this telani version.
/// This blocks the opening of project files from the future in a current version. This enables us to not
/// break compatibility as we introduce new features for the majority of projects.
/// </summary>
public sealed class DatabaseContainsUnsupportedFeaturesException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContainsUnsupportedFeaturesException"/> class.
    /// </summary>
    public DatabaseContainsUnsupportedFeaturesException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContainsUnsupportedFeaturesException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The exception's message</param>
    public DatabaseContainsUnsupportedFeaturesException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContainsUnsupportedFeaturesException"/> class with a specified
    /// error message and an inner exception that is closer to the root cause of the issue.
    /// </summary>
    /// <param name="message">The exception's message</param>
    /// <param name="innerException">An inner exception</param>
    public DatabaseContainsUnsupportedFeaturesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
