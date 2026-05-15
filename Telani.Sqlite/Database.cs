// #define DBTRACEQUERYS
using System.Buffers.Binary;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SQLitePCL;
using Telani.Sqlite.Exceptions;

namespace Telani.Sqlite;

/// <summary>
/// The database, an abstraction of our SQLite database file.
/// </summary>
public sealed partial class Database : IDisposable, IDatabase
{
    private static bool loggerAlreadySetup;

    // the last created instance is statically made accessible here.
    private static Database? lastInstance;

    private readonly SemaphoreSlim translock = new(1);

    private SQLitePCL.sqlite3? conn;
    private bool hasConnection;

    private string? lockFilePath;

    private string? dbPath;

    /// <inheritdoc />
    public string DatabaseName { get; private set; }

    private bool checkForeignKeys;
    private int maxDBVersion;
    private bool readOnly;
    private bool disableBackup;
    private bool createNew;
    private bool configured;

    private bool wroteLockFile;

    private readonly IDatabaseSchema schema;

    private string defaultDatabaseFileExtension = ".db";
    private string[] supportedDatabaseFileExtensions = [".db", ".sqlite"];

    private Action<string, IDictionary<string, string>?>? telemetryCallback;

    private readonly ILogger<Database> _logger;
    private Func<Task>? executeQueryErrorCallback;

    // Use the win32-longpath VFS so paths exceeding MAX_PATH (260) work on Windows.
    private const string SqliteVfs = "win32-longpath";

    /// <summary>
    /// Initializes a new instance of the <see cref="Database"/> class,
    /// using this constructor, you should not call <see cref="Configure" />.
    ///
    /// <paramref name="checkForeignKeys"/> always has to be true for the App to work correctly, it is only available for unit testing.
    /// </summary>
    /// <param name="schema">the database schema, offering migrations</param>
    /// <param name="logger">a logger</param>
    /// <param name="path">the path of the database file</param>
    /// <param name="checkForeignKeys">Checking of foreign key constraints can be disabled for testing purposes</param>
    /// <param name="maxDBVersion">the maximum database version to migrate to, for testing purposes</param>
    /// <param name="readOnly">open the database readonly</param>
    /// <param name="disableBackup">this disables any automatic backup before migrations occur</param>
    /// <param name="createNew">are we creating a new database?</param>
    /// <param name="telemetryCallback">A callback for telemetry events</param>
    /// <param name="defaultDatabaseFileExtension">The default file extension for the database file</param>
    /// <param name="supportedDatabaseFileExtensions">The file extensions supported for the database file</param>
    /// <exception cref="ArgumentException">When <paramref name="createNew"/> and <paramref name="readOnly"/> are set together.</exception>
    public Database(
        IDatabaseSchema schema,
        ILogger<Database> logger,
        string path,
        bool checkForeignKeys = true,
        int maxDBVersion = -1,
        bool readOnly = false,
        bool disableBackup = false,
        bool createNew = false,
        Action<string, IDictionary<string, string>?>? telemetryCallback = null,
        string defaultDatabaseFileExtension = ".db",
        string[]? supportedDatabaseFileExtensions = null)
    {
        lastInstance = this;
        _logger = logger;
        this.telemetryCallback = telemetryCallback;
        this.defaultDatabaseFileExtension = defaultDatabaseFileExtension;
        if (supportedDatabaseFileExtensions != null && supportedDatabaseFileExtensions.Length > 0)
        {
            this.supportedDatabaseFileExtensions = supportedDatabaseFileExtensions;
        }
        if (createNew && readOnly)
        {
            throw new ArgumentException("Incompatible: createNew and readOnly");
        }
        this.schema = schema;
        this.readOnly = readOnly;
        dbPath = path;
        DatabaseName = Path.GetFileNameWithoutExtension(dbPath);
        this.checkForeignKeys = checkForeignKeys;
        this.maxDBVersion = maxDBVersion;
        this.disableBackup = disableBackup;
        this.createNew = createNew;
        configured = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Database"/> class.
    /// You need to call <see cref="Configure" /> after calling this constructor.
    /// This is used for DI.
    /// </summary>
    /// <param name="schema">the database schema, offering migrations</param>
    /// <param name="logger">a logger</param>
    public Database(IDatabaseSchema schema, ILogger<Database> logger)
    {
        lastInstance = this;
        this.schema = schema;
        DatabaseName = "Empty";
        _logger = logger;
    }

    /// <inheritdoc />
    public void Configure(
        string path,
        bool checkForeignKeys = true,
        int maxDBVersion = -1,
        bool isReadOnly = false,
        bool disableBackup = false,
        bool createNew = false,
        Action<string, IDictionary<string, string>?>? telemetryCallback = null,
        string defaultDatabaseFileExtension = ".db",
        string[]? supportedDatabaseFileExtensions = null)
    {
        if (configured)
        {
            throw new InvalidOperationException("Database already configured");
        }
        if (createNew && isReadOnly)
        {
            throw new ArgumentException("Incompatible: createNew and readOnly");
        }

        this.telemetryCallback = telemetryCallback;
        this.defaultDatabaseFileExtension = defaultDatabaseFileExtension;
        if (supportedDatabaseFileExtensions != null && supportedDatabaseFileExtensions.Length > 0)
        {
            this.supportedDatabaseFileExtensions = supportedDatabaseFileExtensions;
        }
        readOnly = isReadOnly;
        dbPath = path;
        DatabaseName = Path.GetFileNameWithoutExtension(dbPath);
        this.checkForeignKeys = checkForeignKeys;
        this.maxDBVersion = maxDBVersion;
        this.disableBackup = disableBackup;
        this.createNew = createNew;
        configured = true;
    }

    private bool CheckFileCompatibility(string path, out int currentVersion)
    {
        using var stream = File.OpenRead(path);
        currentVersion = DatabaseFileAnalyzer.GetDBVersionFromStream(stream);
        return currentVersion == CurrentDatabaseVersion;
    }

    private static void ThrowSqliteError(sqlite3 conn, int myresult, string message = "Could not open Database")
    {
        var errorCode = SQLitePCL.raw.sqlite3_errcode(conn);
        Debug.Assert(errorCode == myresult);
        var extendedErrorCode = SQLitePCL.raw.sqlite3_extended_errcode(conn);
        var errorMsg = GetErrorMessage(conn);
        throw new SQLiteException(SQLiteExceptionSubType.SQLITE_GENERAL, message + ", ResultCode: " + errorCode + " Msg: " + errorMsg + " ExtendedError: " + extendedErrorCode)
        {
            ExtendedErrorCode = extendedErrorCode,
        };
    }

    /// <inheritdoc />
    public string? LocalFolder { get; set; }

    /// <inheritdoc />
    public async Task<int> Initialize(bool skipUnsupportedFeaturesCheck = false, bool skipUnsupportedClientCheck = false, Func<IDatabase, Task>? canOpenValidationCallback = null, Func<Task>? executeQueryErrorCallback = null)
    {
        this.executeQueryErrorCallback = executeQueryErrorCallback;
        if (dbPath is null)
        {
            throw new InvalidOperationException("dbPath not set");
        }

        Debug.WriteLineIf(!checkForeignKeys, "checkForeignKeys can only be false in Unit-Tests.");
        Debug.WriteLineIf(maxDBVersion != -1, "maxDBVersion must be -1 in production");

        int flags;

        if (readOnly)
        {
            flags = SQLitePCL.raw.SQLITE_OPEN_READONLY;

            if (!CheckFileCompatibility(dbPath, out var readOnlyVersion))
            {
                var path = GeneratePathForViewerProject(LocalFolder ?? string.Empty, dbPath);

                File.Copy(dbPath, path, true);

                // This ensures, that the copied file does not have the readonly flag set:
                var fInfo = new FileInfo(path);
                if (fInfo.IsReadOnly)
                {
                    fInfo.IsReadOnly = false;
                }
                await ManuallyApplyDatabaseMigrations(readOnlyVersion, path, schema);
                dbPath = path;
            }
        }
        else
        {
            if (createNew)
            {
                File.Delete(dbPath);
            }
            flags = raw.SQLITE_OPEN_READWRITE | SQLitePCL.raw.SQLITE_OPEN_CREATE;
        }

        var sqliteDBPath = dbPath;

        // if database is on a UNC path ("\\server\share\file.db") it needs to be
        // passed to sqlite with four slashes at the start "\\\\".
        // In C# this results in "\\\\\\\\" because backslashes need to be escaped.s
        // See: http://sqlite.1065341.n5.nabble.com/System-Data-SQLite-and-UNC-Paths-tp72920p72922.html

        // This is no longer the case in .NET 7.0.3 with SQLitePCL.raw 2.1.4 and winsqlite 3.34.1

        // if (sqliteDBPath.StartsWith("\\\\", StringComparison.Ordinal))
        // {
        //    sqliteDBPath = "\\\\" + sqliteDBPath;
        // }
        if (!loggerAlreadySetup)
        {
            loggerAlreadySetup = true;
            SQLitePCL.raw.sqlite3_config_log(ErrorLogUpdateStatic, null);
        }

        var result = SQLitePCL.raw.sqlite3_open_v2(sqliteDBPath, out conn, flags, SqliteVfs);
        if (result != SQLitePCL.raw.SQLITE_OK)
        {
            ThrowSqliteError(conn, result);
        }
        hasConnection = true;

        // not for testing
        if (dbPath != ":memory:" && !readOnly)
        {
            var parentDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            if (parentDir is not null)
            {
                lockFilePath = Path.Combine(parentDir, "~$" + Path.GetFileName(dbPath) + ".lock");
                if (File.Exists(lockFilePath))
                {
                    throw new DatabaseFileLockedException("Database locked.");
                }
                else if (!Debugger.IsAttached)
                {
                    using var writer = File.CreateText(lockFilePath);
                    writer.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine(Environment.MachineName);
                    wroteLockFile = true;

                    // Set hidden flag.
                    // FileAttributes attributes = File.GetAttributes(lockFilePath);
                    // File.SetAttributes(lockFilePath, attributes | FileAttributes.Hidden);
                }
            }
        }
        await ExecuteQuery(string.Format(CultureInfo.InvariantCulture, "PRAGMA foreign_keys = {0};", checkForeignKeys ? "ON" : "OFF"));

        await ExecuteQuery("PRAGMA encoding = \"UTF-8\";");

        // var options = GetCompilerOptions();
        var version = await ExecuteCountQuery("PRAGMA user_version;", null);
        var oldVersion = version;

        var loaded_app_id = await ExecuteCountQuery("PRAGMA application_id;", null);

        // The default for 3rd party apps would be loaded_app_id == 0, this is also true for super old telani projects. But as the old projects get updated, the warning will disappear.
        // So we show this warning for 0, and risk potentially showing this warning once (until migrations are executed) for super old projects.
        if (!createNew && loaded_app_id != schema.AppId && !skipUnsupportedClientCheck)
        {
            throw new DatabaseModifiedWithUnsupportedException("Project file is not compatible with this application. Application ID does not match.");
        }

        var migrations = schema.GetMigrations();
        if (!readOnly)
        {
            // Add Database updates here. Simply add an async Action to the migrations list.
            // Migrations each get wrapped in a transaction and the user_version gets updated automatically.

            // Every database schema update needs to be incremental for backwards compatibility.
            // For new versions you should also update DatabaseTests.
            if (dbPath != ":memory:" && version < migrations.Last().VersionNumber && !disableBackup && version > 1)
            {
                await Backup();
            }

            foreach (var migration in migrations)
            {
                if (version < migration.VersionNumber)
                {
                    telemetryCallback?.Invoke("ApplyMigration", new Dictionary<string, string>()
                    {
                        { "DBVersion", version.ToString(CultureInfo.InvariantCulture) },
                        { "MigrationVersion", migration.VersionNumber.ToString(CultureInfo.InvariantCulture) },
                    });
                    await ApplyMigration(migration);
                    version = migration.VersionNumber;
                    if (maxDBVersion == migration.VersionNumber)
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            if (version < migrations.Count)
            {
                throw new MigrationsNotPossibleBecauseReadOnlyException();
            }
        }

        var value = await ExecuteCountQuery("PRAGMA foreign_keys");
        Debug.Assert(value == 1 || !checkForeignKeys);

        if (!CanOpenDatabaseVersion(version, CurrentDatabaseVersion))
        {
            throw new DatabaseModifiedByNewerVersionException();
        }

        if (!skipUnsupportedFeaturesCheck && await DoesDatabaseContainUnsupportedFeatures())
        {
            throw new DatabaseContainsUnsupportedFeaturesException();
        }

        if (!skipUnsupportedFeaturesCheck)
        {
            var res = canOpenValidationCallback?.Invoke(this);
            if (res != null)
            {
                await res;
            }
        }

        if (!readOnly)
        {
            await Optimize();
        }

        // return the old Version, so we can decide in UI to run some Userinterface migrations.
        return oldVersion;
    }

    private static void ErrorLogUpdateStatic(object user_data, int errorCode, utf8z msg)
    {
        // This log is called my sqlite when it wants to log something.
        // It writes warnings about queries to this log. If something is written to this log it is worth investigating.

        // Note: user_data currently is null currently.

        // var message = msg.utf8_to_string();
        // Debug.Fail($"SQLite Error: {errorCode} {message}");

        // We want to call the ErrorLogUpdate method below, but there is some problem with disposed database instances.
        // Initially we used a non-static method, but it suffers from a disposed hook exception.
        // We tried passing the database as the user_data parameter, but this does not work either.
        // So now we keep the last instance in a static field, it gets emptied when the database is disposed.
        // This should mean that usually when this method is called an instance is available in the static field.
        lastInstance?.ErrorLogUpdate(errorCode, msg);
    }

    private void ErrorLogUpdate(int errorCode, utf8z msg)
    {
        var message = msg.utf8_to_string();
        _logger.LogError("SQLite Error: {ErrorCode} {Message}", errorCode, message);
        telemetryCallback?.Invoke("SQLiteNativeError", new Dictionary<string, string>()
        {
            { "ErrorCode", errorCode.ToString(CultureInfo.InvariantCulture) },
            { "Message", message },
        });
        Debug.Fail($"SQLite Error: {errorCode} {message}");
    }

    private async Task ApplyMigration(DBMigration migration)
    {
        if (migration.DisableForeignKeysForMigration)
        {
            await ExecuteQuery("PRAGMA foreign_keys=OFF");
        }
        await StartTransaction(immediate: true);
        foreach (var query in migration.Statements)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                await ExecuteQuery(query);
            }
        }
        if (migration.DisableForeignKeysForMigration)
        {
            using var reader = await ExecuteSelectQuery("PRAGMA foreign_key_check;");
            if (await reader.ReadAsync())
            {
                throw new ForeignKeyConstraintViolationException();
            }
        }
        await ExecuteQuery("PRAGMA user_version = " + migration.VersionNumber);
        await CommitTransaction();
        if (migration.DisableForeignKeysForMigration && checkForeignKeys)
        {
            await ExecuteQuery("PRAGMA foreign_keys=ON");
        }
    }

    private string GeneratePathForViewerProject(string localFolder, string dbPath)
    {
        var filename = SHA256.HashData(Encoding.UTF8.GetBytes(dbPath.ToUpperInvariant()));
        var encoded = System.Convert.ToBase64String(filename).Replace('/', '_');
        var path = Path.Combine(localFolder, encoded + defaultDatabaseFileExtension);
        return path;
    }

    private static string GetErrorMessage(sqlite3 myConn)
    {
        var errorMsg_utf8 = SQLitePCL.raw.sqlite3_errmsg(myConn);
        return errorMsg_utf8.utf8_to_string();
    }

    private static async Task ManuallyApplyDatabaseMigrations(int readOnlyVersion, string path, IDatabaseSchema schema)
    {
        var myflags = raw.SQLITE_OPEN_READWRITE;
        var myresult = SQLitePCL.raw.sqlite3_open_v2(path, out var myConn, myflags, SqliteVfs);

        if (myresult != SQLitePCL.raw.SQLITE_OK)
        {
            ThrowSqliteError(myConn, myresult);
        }
        using (myConn)
        {
            await InternalExecuteQuery(myConn, "PRAGMA encoding = \"UTF-8\";");
            var mymigrations = schema.GetMigrations();
            foreach (var migration in mymigrations)
            {
                if (readOnlyVersion < migration.VersionNumber)
                {
                    await ApplyMigrationManually(myConn, migration);
                    readOnlyVersion = migration.VersionNumber;
                }
            }
        }
    }

    private static async Task ApplyMigrationManually(sqlite3 myConn, DBMigration migration)
    {
        if (migration.DisableForeignKeysForMigration)
        {
            await InternalExecuteQuery(myConn, "PRAGMA foreign_keys=OFF");
        }
        await InternalExecuteQuery(myConn, "BEGIN;");
        foreach (var query in migration.Statements)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                await InternalExecuteQuery(myConn, query);
            }
        }
        await InternalExecuteQuery(myConn, "PRAGMA user_version = " + migration.VersionNumber);
        await InternalExecuteQuery(myConn, "COMMIT;");
        if (migration.DisableForeignKeysForMigration)
        {
            await InternalExecuteQuery(myConn, "PRAGMA foreign_keys=ON");
        }
    }

    private async Task<bool> DoesDatabaseContainUnsupportedFeatures()
    {
        // this checks if any plan elements are on more then one floor:
        using var reader = await ExecuteSelectQuery("SELECT count(*) FROM PlanElementLocation GROUP BY PlanElementId HAVING count(FloorId) > 1 LIMIT 1;");
        while (reader.Read())
        {
            return true;
        }
        try
        {
            using var frozenReader = await ExecuteSelectQuery("SELECT Value FROM Project WHERE Property = 'ProjectFrozen' LIMIT 1;");
            while (frozenReader.Read())
            {
                if (!frozenReader.IsDBNull(0) && !string.IsNullOrEmpty(frozenReader.GetString(0)))
                {
                    return true;
                }
            }
        }
        catch (SQLiteException)
        {
        }

        return false;
    }

    /// <summary>
    /// Can a given database version be opened with this version of the app?
    /// </summary>
    /// <param name="dbVersion">the version to check</param>
    /// <param name="supportedVersion">the current version native database version</param>
    /// <returns></returns>
    public static bool CanOpenDatabaseVersion(int dbVersion, int supportedVersion)
    {
        if (dbVersion < 100 && dbVersion > supportedVersion)
        {
            return false;
        }
        else if (dbVersion >= 100 && ((dbVersion / 100) < (supportedVersion / 100) || (dbVersion / 100) >= ((supportedVersion / 100) + 1)))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// The current version of the database. This is also the version that new projects will be.
    /// </summary>
    public int CurrentDatabaseVersion => schema.GetMigrations().Last().VersionNumber;

    /// <inheritdoc />
    public string DatabasePath => dbPath ?? string.Empty;

    /// <summary>
    /// The timeout for transactions, can be changed for testing.
    /// </summary>
    public int TransactionTimeout { get; set; } = 50_000;

    /// <inheritdoc />
    public async Task StartTransaction(bool immediate = false)
    {
        var gotLock = await translock.WaitAsync(TransactionTimeout); // 10s timeout
        if (!gotLock)
        {
            throw new TimeoutException("Could not acquire transaction lock in a reasonable time.");
        }
        await ExecuteQuery(immediate ? "BEGIN IMMEDIATE;" : "BEGIN;");
    }

    /// <inheritdoc />
    public async Task CommitTransaction(bool transactionWasCancelled = false)
    {
        try
        {
            if (!transactionWasCancelled)
            {
                await ExecuteQuery("COMMIT;");
            }
        }
        finally
        {
            translock.Release();
        }
    }

    /// <inheritdoc />
    public bool InTransaction
    {
        get
        {
            if (!hasConnection)
            {
                return false;
            }
            var result = raw.sqlite3_get_autocommit(conn);

            // These asserts are a bit aggressive, when we are checking if a transaction is active, then likely to cleanup a sqlite error.
            // One of the things we need to clean up after an error is the transaction lock, so if we fail this assertion,
            // it is likely that the error handling code currently being executed is about to fix the lock as well.
            if (result == 0)
            {
                Debug.Assert(translock.CurrentCount == 0);
            }
            else
            {
                Debug.Assert(translock.CurrentCount > 0);
            }
            return result == 0;
        }
    }

#if DBTRACEQUERYS
        private Stopwatch StartDebugTimer()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            return stopWatch;
        }

        private void StopDebugTimer(Stopwatch stopWatch, string query)
        {
            stopWatch.Stop();
            _logger.LogInformation("{Query} Time: {Time}ms", query, stopWatch.ElapsedMilliseconds);
        }
#endif

    /// <inheritdoc />
    public Task ExecuteQuery(string query) => ExecuteQuery(query, null);

    /// <inheritdoc />
    public async Task ExecuteQuery(string query, DbParameter[]? parameters)
    {
        // check if db is really open?
        if (hasConnection)
        {
#if DBTRACEQUERYS
                var stopWatch = StartDebugTimer();
#endif

            var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("connection not net"))
            {
                CommandText = query,
            };
            if (parameters is not null)
            {
                command.Parameters.AddRange(parameters);
            }
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (SQLiteException ex) when (ex.SqlLiteErrorCode == 8)
            {
                var res = executeQueryErrorCallback?.Invoke();
                if (res != null)
                {
                    await res;
                }
                throw;
            }
#if DBTRACEQUERYS
                StopDebugTimer(stopWatch, query);
#endif
        }
    }

    private static async Task InternalExecuteQuery(sqlite3 conn, string query, DbParameter[]? parameters = null)
    {
        var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("connection not net"))
        {
            CommandText = query,
        };
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters);
        }
        await command.ExecuteNonQueryAsync();
    }

    private void ExecuteQuerySync(string query)
    {
#if DBTRACEQUERYS
            var stopWatch = StartDebugTimer();
#endif
        var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("Conn not set"))
        {
            CommandText = query,
        };
        command.ExecuteNonQuery();
#if DBTRACEQUERYS
            StopDebugTimer(stopWatch, query);
#endif
    }

    private bool isDisposed;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose, according to the dispose-pattern. May take a little
    /// bit because it gives transactions a moment to complete before interrupting them.
    /// </summary>
    /// <param name="onlyNative">always set to true, unless the call is from the finalizer.</param>
    private void Dispose(bool onlyNative)
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
        var gotLock = translock.Wait(2000);
        if (!gotLock)
        {
            translock.Release();
            ExecuteQuerySync("ROLLBACK;");
        }
        if (lockFilePath is not null && wroteLockFile)
        {
            DeleteLockFile();
        }
        hasConnection = false;
        if (conn is not null)
        {
            var res = SQLitePCL.raw.sqlite3_close_v2(conn);
            if (res != SQLitePCL.raw.SQLITE_OK)
            {
                throw new SQLiteException(SQLiteExceptionSubType.SQLITE_GENERAL, "Could not close database");
            }
            conn.Dispose();
            conn = null;
        }
        translock?.Dispose();
        lastInstance = null;
    }

    /// <inheritdoc />
    public async Task<long> InsertQuery(string query, DbParameter[]? parameters)
    {
        await ExecuteQuery(query, parameters);
        return SQLitePCL.raw.sqlite3_last_insert_rowid(conn);
    }

    /// <inheritdoc />
    public async Task<int> ExecuteCountQuery(string query, DbParameter[]? parameters = null)
    {
#if DBTRACEQUERYS
            var stopWatch = StartDebugTimer();
#endif

        // careful: parameters can be null;
        var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("conn not set up"))
        {
            CommandText = query,
        };
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters);
        }
        using var reader = await command.ExecuteReaderAsync();
        Debug.Assert(reader.FieldCount == 1);
        Debug.Assert(reader.GetDataTypeName(0) is "int" or "null");
        var res = await reader.ReadAsync();
        Debug.Assert(res);
#if DBTRACEQUERYS
                StopDebugTimer(stopWatch, query);
#endif
        var count = reader.GetInt32(0);

        Debug.Assert(!await reader.ReadAsync());

        return count;
    }

    /// <inheritdoc />
    public async Task<string?> ExecuteStringQuery(string query, DbParameter[]? parameters = null)
    {
#if DBTRACEQUERYS
        var stopWatch = StartDebugTimer();
#endif

        // careful: parameters can be null;
        var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("conn not set up"))
        {
            CommandText = query,
        };
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters);
        }
        using var reader = await command.ExecuteReaderAsync();
        Debug.Assert(reader.FieldCount == 1);
        Debug.Assert(reader.GetDataTypeName(0) is "string" or "null");
        var res = await reader.ReadAsync();
        if (!res)
        {
            return null;
        }
        Debug.Assert(res);
#if DBTRACEQUERYS
        StopDebugTimer(stopWatch, query);
#endif
        var count = reader.GetString(0);

        Debug.Assert(!await reader.ReadAsync());

        return count;
    }

    /// <inheritdoc />
    public async Task<long[]> ExecuteLongArrayQuery(string query, DbParameter[]? parameters)
    {
#if DBTRACEQUERYS
            var stopWatch = StartDebugTimer();
#endif
        var result = new List<long>();
        var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("conn not set up"))
        {
            CommandText = query,
        };
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters);
        }
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetInt32(0));
            }
        }
#if DBTRACEQUERYS
            StopDebugTimer(stopWatch, query);
#endif
        return [.. result];
    }

    /// <inheritdoc />
    public async Task<DbDataReader> ExecuteSelectQuery(string query, DbParameter[]? parameters)
    {
#if DBTRACEQUERYS
            var stopWatch = StartDebugTimer();
#endif
        var command = new SQLiteCommand(conn ?? throw new InvalidOperationException("conn not set up"))
        {
            CommandText = query,
        };
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters);
        }
        var reader = await command.ExecuteReaderAsync();
#if DBTRACEQUERYS
            StopDebugTimer(stopWatch, query);
#endif

        return reader;
    }

    /// <inheritdoc />
    public async Task<DbDataReader> ExecuteSelectQuery(string query) => await ExecuteSelectQuery(query, null);

    /// <inheritdoc />
    public async Task<string> Backup()
    {
        if (dbPath is null)
        {
            throw new InvalidOperationException("dbPath not set");
        }
        return await Task.Run(async () =>
        {
            int flags = SQLitePCL.raw.SQLITE_OPEN_READWRITE | SQLitePCL.raw.SQLITE_OPEN_CREATE;
            var backupPath = BuildBackupPath(dbPath, DateTime.Now);

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            var res = raw.sqlite3_open_v2(backupPath, out sqlite3 backup_db, flags, SqliteVfs);
            if (res != SQLitePCL.raw.SQLITE_OK)
            {
                ThrowSqliteError(backup_db, res);
            }
            sqlite3_backup backup = raw.sqlite3_backup_init(backup_db, "main", conn, "main") ?? throw new SQLiteException(SQLiteExceptionSubType.SQLITE_CONSTRAINT, "Could not init backup");
            res = raw.sqlite3_backup_step(backup, -1);
            if (res != SQLitePCL.raw.SQLITE_DONE)
            {
                ThrowSqliteError(backup_db, res, "Could not step backup");
            }
            if (backup is not null)
            {
                var result = raw.sqlite3_backup_finish(backup);
                if (result != SQLitePCL.raw.SQLITE_OK)
                {
                    ThrowSqliteError(backup_db, res, "Could not clean up backup.");
                }
            }

            var command = new SQLiteCommand(backup_db)
            {
                CommandText = "INSERT OR REPLACE INTO project VALUES('MarkedAsFinal', @value);",
            };
            command.Parameters.AddRange(
                new List<DbParameter>()
                {
                    new SQLiteParameter("@value", DateTime.Now.Ticks),
                });
            await command.ExecuteNonQueryAsync();

            if (backup_db is not null)
            {
                var result = raw.sqlite3_close_v2(backup_db);
                if (result != SQLitePCL.raw.SQLITE_OK)
                {
                    throw new SQLiteException(SQLiteExceptionSubType.SQLITE_GENERAL, "Could not close backup db");
                }
            }
            return backupPath;
        });
    }

    /// <summary>
    /// Get the SQLite library compiler options.
    /// For dev purposes.
    /// </summary>
    /// <seealso href="https://www.sqlite.org/pragma.html#pragma_compile_options" />
    /// <seealso href="https://www.sqlite.org/compile.html"/>
    /// <returns>a string representation of the compile options.</returns>
    public async Task<string> GetCompilerOptions()
    {
        var str = new StringBuilder();
        using (var reader = await ExecuteSelectQuery("PRAGMA compile_options;"))
        {
            while (await reader.ReadAsync())
            {
                str.AppendLine(reader.GetString(0));
            }
        }
        return str.ToString();
    }

    /// <inheritdoc />
    public Task Optimize()
        => Task.Run(async () =>
        {
            await StartTransaction(immediate: true);

            using (var reader = await ExecuteSelectQuery("pragma integrity_check", null))
            {
                await reader.ReadAsync();
                if (reader.GetString(0) != "ok")
                {
                    throw new CorruptDatabaseFileException("Integrity check failed");
                }
            }
            var freeList = await ExecuteCountQuery("pragma freelist_count", null);
            var page_count = await ExecuteCountQuery("pragma page_count", null);
            var page_size = await ExecuteCountQuery("pragma page_size", null);
            try
            {
                await CommitTransaction();
            }
            catch (SQLiteException ex) when (ex.SqlLiteErrorCode == 1 && ex.Message.Contains("no transaction is active", StringComparison.OrdinalIgnoreCase))
            {
                // Optimize is best-effort. If Dispose (or another path) rolled back the
                // transaction while we were running, the COMMIT is a no-op; swallow it.
                // translock is already released by CommitTransaction's finally.
            }

            // at least ~1MB of unused space
            if (freeList * page_size > 4_000_000)
            {
                await StartTransaction(immediate: true);
                await ExecuteQuery("pragma auto_vacuum=2");
                await CommitTransaction();
                await ExecuteQuery("VACUUM");
            }
        });

    /// <inheritdoc />
    public bool DeleteLockFile()
    {
        if (lockFilePath is not null)
        {
            try
            {
                File.Delete(lockFilePath);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
        return false;
    }

    [GeneratedRegex(@" \d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}$")]
    private static partial Regex BackupTimestampSuffix();

    // Builds the backup destination path next to the source database by appending
    // a " yyyy-MM-dd HH-mm-ss" timestamp. Any existing trailing timestamp suffixes
    // on the source name are stripped first so successive backups of an already-
    // backed-up file don't grow the filename unboundedly. Unknown extensions fall
    // back to the default database file extension.
    internal string BuildBackupPath(string dbPath, DateTime now)
    {
        var directory = Path.GetDirectoryName(dbPath) ?? string.Empty;
        var extension = Path.GetExtension(dbPath);
        if (!supportedDatabaseFileExtensions.Contains(extension.ToLowerInvariant()))
        {
            extension = defaultDatabaseFileExtension;
        }

        var baseName = Path.GetFileNameWithoutExtension(dbPath).Trim();
        while (BackupTimestampSuffix().IsMatch(baseName))
        {
            baseName = BackupTimestampSuffix().Replace(baseName, string.Empty).TrimEnd();
        }

        var filename = baseName + " " + now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.CurrentCulture) + extension;
        return Path.Combine(directory, filename);
    }
}
