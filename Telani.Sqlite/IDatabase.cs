using System.Data.Common;

namespace Telani.Sqlite;

/// <summary>
/// The database, an abstraction of our sqlite database file.
/// </summary>
public interface IDatabase : IDisposable
{
    /// <summary>
    /// Configure the database in dependency injection contexts, used together with constructor with few arguments.
    /// </summary>
    /// <param name="path">the path of the database file</param>
    /// <param name="checkForeignKeys">Checking of foreign key constraints can be disabled for testing purposes</param>
    /// <param name="maxDBVersion">the maximum database version to migrate to, for testing purposes</param>
    /// <param name="isReadOnly">open the database readonly</param>
    /// <param name="disableBackup">this disables any automatic backup before migrations occur</param>
    /// <param name="createNew">are we creating a new database?</param>
    /// <param name="telemetryCallback">A callback for telemetry events</param>
    /// <param name="defaultDatabaseFileExtension">The default file extension for the database file</param>
    /// <param name="supportedDatabaseFileExtensions">The file extensions supported for the database file</param>
    void Configure(
        string path,
        bool checkForeignKeys = true,
        int maxDBVersion = -1,
        bool isReadOnly = false,
        bool disableBackup = false,
        bool createNew = false,
        Action<string, IDictionary<string, string>?>? telemetryCallback = null,
        string defaultDatabaseFileExtension = ".db",
        string[]? supportedDatabaseFileExtensions = null);

    /// <summary>
    /// The name of the database. Usually from the filename.
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// The path of the database file or an empty string if the path was not set yet.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// The local folder that readonly-projects can be copied to.
    /// </summary>
    string? LocalFolder { get; set; }

    /// <summary>
    /// True if the database is currently in a transaction.
    /// </summary>
    bool InTransaction { get; }

    /// <summary>
    /// Initialize the database. Opens the connection to the database.
    /// </summary>
    /// <param name="skipUnsupportedFeaturesCheck">In tests it is sometimes needed to skip the unsupported features check.</param>
    /// <param name="skipUnsupportedClientCheck">We can override the check if the application was modified by 3rd-party applications.</param>
    /// <param name="canOpenValidationCallback">A callback that is called before completing the opening of the database, skipped with <paramref name="skipUnsupportedFeaturesCheck"/></param>
    /// <param name="executeQueryErrorCallback">A callback that is called when an error occurs while executing a query.</param>
    /// <returns>the old database version, so UI can suggest migrations.</returns>
    Task<int> Initialize(bool skipUnsupportedFeaturesCheck = false, bool skipUnsupportedClientCheck = false, Func<IDatabase, Task>? canOpenValidationCallback = null, Func<Task>? executeQueryErrorCallback = null);

    /// <summary>
    /// Executes a query and does not expect a result. <paramref name="parameters"/> will used to fill the query in a safe way.
    /// </summary>
    /// <param name="query">the sql query.</param>
    /// <param name="parameters">parameters to fill the query</param>
    /// <returns>a task to await the completion</returns>
    Task ExecuteQuery(string query, DbParameter[]? parameters);

    /// <summary>
    /// Executes a query and does not expect a result.
    /// </summary>
    /// <param name="query">the sql query.</param>
    /// <returns>a task to await the completion</returns>
    Task ExecuteQuery(string query);

    /// <summary>
    /// Insert a value into the database. This will execute the query
    /// and then get the last inserted rowid to return.
    /// </summary>
    /// <param name="query">the sql query</param>
    /// <param name="parameters">any parameters for the query</param>
    /// <returns>the row id of the just inserted item.</returns>
    Task<long> InsertQuery(string query, DbParameter[]? parameters);

    /// <summary>
    /// Execute a query that returns a scalar int. Often this is a
    /// count query, but the type of query does not matter.
    /// </summary>
    /// <remarks>
    /// In debug mode this method asserts that only one row is returned from the query.
    /// This row also validates that the first column is of type int or null in debug mode.
    /// </remarks>
    /// <param name="query">the sql query</param>
    /// <param name="parameters">any parameters for the query</param>
    /// <returns>the int value the query returned.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    Task<int> ExecuteCountQuery(string query, DbParameter[]? parameters);

    /// <summary>
    /// Executes a query that returns a array of longs. The query returns one long per row.
    /// </summary>
    /// <param name="query">the sql query</param>
    /// <param name="parameters">any parameters for the query</param>
    /// <returns>an array of longs as the query returned, may be empty.</returns>
    Task<long[]> ExecuteLongArrayQuery(string query, DbParameter[]? parameters);

    /// <summary>
    /// Execute a select query on the database.
    /// </summary>
    /// <param name="query">the sql query</param>
    /// <param name="parameters">any parameters for the query</param>
    /// <returns>a reader to read the results</returns>
    Task<DbDataReader> ExecuteSelectQuery(string query, DbParameter[]? parameters);

    /// <summary>
    /// Execute a select query on the database.
    /// </summary>
    /// <param name="query">the sql query</param>
    /// <returns>a reader to read the results</returns>
    Task<DbDataReader> ExecuteSelectQuery(string query);

    /// <summary>
    /// Commit a transaction, basically calls "Commit" on the database.
    /// </summary>
    /// <param name="transactionWasCancelled">If this is true, then we assume that the sqlite transaction
    /// was cancelled and only the telani-internal logic needs to end the transaction</param>
    /// <returns>task to await completion</returns>
    Task CommitTransaction(bool transactionWasCancelled = false);

    /// <summary>
    /// Start a transaction, basically calls "Begin" on the database.
    /// </summary>
    /// <param name="immediate">When true an immediate transaction is started. False a normal transaction is started. An immediate transaction acquires a write-lock as If the transaction immediately </param>
    /// <returns>task to await the start of the transaction</returns>
    /// <exception cref="TimeoutException">database was locked for  too long</exception>
    Task StartTransaction(bool immediate = false);

    /// <summary>
    /// Backup the database to a separate file, using the sqlite online backup api.
    /// </summary>
    /// <returns>a task to await completion that resolves to the absolute filepath of the created backup.</returns>
    Task<string> Backup();

    /// <summary>
    /// Optimize the database file. Includes integrity checks, might include a vacuum run.
    /// </summary>
    /// <returns>task to await completion</returns>
    /// <exception cref="Exception"></exception>
    Task Optimize();

    /// <summary>
    /// Delete the lock file locking the current database file.
    /// </summary>
    /// <returns>true on success, false otherwise</returns>
    bool DeleteLockFile();

    /// <summary>
    /// Gets a single string value from the database.
    /// </summary>
    /// <param name="query">The sql query</param>
    /// <param name="parameters">Any parameters for the query</param>
    /// <returns>If the query returns at least one row then the first value of the first
    /// row will be returned as a string, if no row is returned null will be returned.</returns>
    Task<string?> ExecuteStringQuery(string query, DbParameter[]? parameters = null);
}
