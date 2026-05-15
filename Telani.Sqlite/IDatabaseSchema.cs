namespace Telani.Sqlite;

/// <summary>
/// The database schema for our SQLite-based project file.
/// The schema is established through a number of migrations.
/// These migrations need to be applied sequentially to achieve the current schema.
///
/// </summary>
public interface IDatabaseSchema
{
    /// <summary>
    /// The application ID. This ID gets written into the SQLite header section by the `PRAGMA application_id`.
    /// This helps us identify files created by telani.
    ///
    /// We might warn people that files created by other applications are not supported.
    /// </summary>
    uint AppId { get; }

    /// <summary>
    /// Get the current schema as a series of transactions.
    ///
    ///
    /// It is tempting to simplify or 'fix' issues in previous schema versions, however
    /// this needs to done *very* carefully: We can assume that project files
    /// with any schema version exist. The forward compatibility of all these
    /// projects needs to be considered.
    ///
    /// We achieve backwards compatibility (meaning a newer version of the app can open an older project),
    /// by applying the migrations needed to bring the file up-to-date with the latest schema.
    ///
    /// Forward compatibility (meaning older application versions can open a project edited by new version.),
    /// was not supported until schema version 100. Now it is assumed that any 1xx database can be opened by any application
    /// expecting a 1xx schema.
    ///
    /// </summary>
    /// <returns>a List of migrations. Each contains a version number and a list of sql statements as strings.</returns>
    List<DBMigration> GetMigrations();
}
