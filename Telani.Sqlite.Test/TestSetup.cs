namespace Telani.Sqlite.Test;

[TestClass]
public static class TestSetup
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        // The library depends only on SQLitePCLRaw.core and expects the consumer to register the
        // native provider. Register e_sqlite3 (the same provider the app uses) once for the whole
        // test assembly so Database can open connections.
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
    }
}
