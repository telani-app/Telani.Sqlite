using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Telani.Sqlite.Test;

[TestClass]
public sealed class DatabaseBackupFilenameTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 15, 10, 30, 45);
    private static readonly string Stamp = FixedNow.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.CurrentCulture);

    private sealed class EmptySchema : IDatabaseSchema
    {
        public uint AppId => 0;
        public List<DBMigration> GetMigrations() => [];
    }

    private static Database CreateDatabase() => new(
        new EmptySchema(),
        new NullLogger<Database>(),
        ":memory:",
        defaultDatabaseFileExtension: ".telani",
        supportedDatabaseFileExtensions: [".telani", ".telani-concept"]);

    [TestMethod]
    public void PlainNameGetsTimestampAppended()
    {
        using var db = CreateDatabase();
        var dir = Path.Combine("C:", "projects");
        var source = Path.Combine(dir, "MyProject.telani");
        var expected = Path.Combine(dir, $"MyProject {Stamp}.telani");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }

    [TestMethod]
    public void ExistingTimestampSuffixIsReplaced()
    {
        using var db = CreateDatabase();
        var dir = Path.Combine("C:", "projects");
        var source = Path.Combine(dir, "MyProject 2024-07-03 11-16-10.telani");
        var expected = Path.Combine(dir, $"MyProject {Stamp}.telani");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }

    [TestMethod]
    public void MultipleStackedTimestampSuffixesAreAllReplaced()
    {
        using var db = CreateDatabase();
        var dir = Path.Combine("H:", "Projekte");
        var source = Path.Combine(dir, "5767-AST-BFM-001-telani-2024-02-05 2024-07-03 11-16-10 2025-05-27 08-35-50 2026-04-08 09-44-47.telani");
        var expected = Path.Combine(dir, $"5767-AST-BFM-001-telani-2024-02-05 {Stamp}.telani");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }

    [TestMethod]
    public void ConceptExtensionIsPreserved()
    {
        using var db = CreateDatabase();
        var source = Path.Combine("C:", "p", "Concept.telani-concept");
        var expected = Path.Combine("C:", "p", $"Concept {Stamp}.telani-concept");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }

    [TestMethod]
    public void UnknownExtensionFallsBackToTelani()
    {
        using var db = CreateDatabase();
        var source = Path.Combine("C:", "p", "Foo.sqlite");
        var expected = Path.Combine("C:", "p", $"Foo {Stamp}.telani");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }

    [TestMethod]
    public void DateOnlyInNameIsNotStripped()
    {
        using var db = CreateDatabase();
        var source = Path.Combine("C:", "p", "Project-2024-02-05.telani");
        var expected = Path.Combine("C:", "p", $"Project-2024-02-05 {Stamp}.telani");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }

    [TestMethod]
    public void TimestampInMiddleOfNameIsNotStripped()
    {
        using var db = CreateDatabase();
        var source = Path.Combine("C:", "p", "Project 2024-07-03 11-16-10 Final.telani");
        var expected = Path.Combine("C:", "p", $"Project 2024-07-03 11-16-10 Final {Stamp}.telani");
        Assert.AreEqual(expected, db.BuildBackupPath(source, FixedNow));
    }
}
