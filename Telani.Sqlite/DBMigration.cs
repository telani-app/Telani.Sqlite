namespace Telani.Sqlite;

/// <summary>
/// Represents a single migration, it can contain a large number of sql statements.
///
/// A migration will be executed in a transaction.
/// </summary>
/// <param name="VersionNumber">The version number of this migration.</param>
/// <param name="Statements">A list of strings containing sql statements to execute to apply this migration.</param>
/// <param name="DisableForeignKeysForMigration">Before running this migration the foreign key support in SQLite will be suspended. After the migration the support will be re-enabled automatically.</param>
public sealed record DBMigration(int VersionNumber, List<string> Statements, bool DisableForeignKeysForMigration = false);
