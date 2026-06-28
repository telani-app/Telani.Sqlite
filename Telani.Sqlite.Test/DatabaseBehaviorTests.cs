using Microsoft.Extensions.Logging.Abstractions;

namespace Telani.Sqlite.Test;

/// <summary>
/// Tests for the transaction/locking behaviour: busy_timeout, ExecuteSingle, the requirement
/// that ExecuteQuery runs inside a transaction, and that the transaction lock is always released
/// (on statement failure and after a backup).
/// </summary>
[TestClass]
public sealed class DatabaseBehaviorTests
{
    private sealed class TestSchema : IDatabaseSchema
    {
        public uint AppId => 0;

        public List<DBMigration> GetMigrations() =>
        [
            new DBMigration(1,
            [
                "CREATE TABLE T (Id INTEGER PRIMARY KEY AUTOINCREMENT, V TEXT);",
                // Backup() writes a 'MarkedAsFinal' row into a 'project' table, so it must exist.
                "CREATE TABLE project (Property TEXT PRIMARY KEY, Value);",
            ]),
        ];
    }

    private static async Task<Database> CreateInitializedAsync(string path = ":memory:")
    {
        var db = new Database(new TestSchema(), new NullLogger<Database>(), path);
        await db.Initialize(skipUnsupportedFeaturesCheck: true, skipUnsupportedClientCheck: true);

        // Keep a regression that leaks translock from hanging the test for the full default timeout;
        // instead it fails fast with a TimeoutException.
        db.TransactionTimeout = 2000;
        return db;
    }

    [TestMethod]
    public async Task InitializeSetsBusyTimeout()
    {
        using var db = await CreateInitializedAsync();

        using var reader = await db.ExecuteSelectQuery("PRAGMA busy_timeout");
        Assert.IsTrue(await reader.ReadAsync(testContext.CancellationToken));
        Assert.AreEqual(5000, reader.GetInt32(0));
    }

    [TestMethod]
    public async Task ExecuteSingleCommitsWriteAndReleasesLock()
    {
        using var db = await CreateInitializedAsync();

        await db.ExecuteSingle("INSERT INTO T (V) VALUES ('a')");
        // The second call can only acquire translock if the first one released it.
        await db.ExecuteSingle("INSERT INTO T (V) VALUES (@v)", [new SQLiteParameter("@v", "b")]);

        Assert.AreEqual(2, await db.ExecuteCountQuery("SELECT count(*) FROM T"));
    }

    [TestMethod]
    public async Task ExecuteSingleReleasesLockWhenStatementFails()
    {
        using var db = await CreateInitializedAsync();

        SQLiteException? caught = null;
        try
        {
            await db.ExecuteSingle("INSERT INTO NoSuchTable (V) VALUES ('x')");
        }
        catch (SQLiteException ex)
        {
            caught = ex;
        }
        Assert.IsNotNull(caught, "a failing statement should surface a SQLiteException");

        // If ExecuteSingle had leaked translock on the failure path, this would block for
        // TransactionTimeout and then throw TimeoutException. It should simply succeed.
        await db.ExecuteSingle("INSERT INTO T (V) VALUES ('ok')");
        Assert.AreEqual(1, await db.ExecuteCountQuery("SELECT count(*) FROM T"));
    }

    [TestMethod]
    public async Task ExecuteQueryWorksInsideTransaction()
    {
        using var db = await CreateInitializedAsync();

        await db.StartTransaction(immediate: true);
        await db.ExecuteQuery("INSERT INTO T (V) VALUES ('x')");
        await db.CommitTransaction();

        Assert.AreEqual(1, await db.ExecuteCountQuery("SELECT count(*) FROM T"));
    }

    [TestMethod]
    public async Task BackupReleasesLockSoWritesContinue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"telani-sqlite-test-{Guid.NewGuid():N}.telani");
        var lockPath = Path.Combine(Path.GetTempPath(), "~$" + Path.GetFileName(path) + ".lock");
        string? backupPath = null;

        var db = await CreateInitializedAsync(path);
        try
        {
            await db.ExecuteSingle("INSERT INTO T (V) VALUES ('before')");

            backupPath = await db.Backup();
            Assert.IsTrue(File.Exists(backupPath), "backup file should be created");

            // Backup holds translock for its duration; afterwards a write must still succeed
            // (and quickly), proving the lock was released.
            await db.ExecuteSingle("INSERT INTO T (V) VALUES ('after')");
            Assert.AreEqual(2, await db.ExecuteCountQuery("SELECT count(*) FROM T"));
        }
        finally
        {
            db.Dispose();
            foreach (var p in new[] { path, lockPath, backupPath })
            {
                if (p is not null && File.Exists(p))
                {
                    File.Delete(p);
                }
            }
        }
    }

    private readonly TestContext testContext;

    public DatabaseBehaviorTests(TestContext testContext)
    {
        this.testContext = testContext;
    }
}
