using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Telani.Sqlite;

internal sealed partial class SQLiteReader : DbDataReader
{
    private readonly SQLitePCL.sqlite3_stmt _statement;

    public SQLiteReader(SQLitePCL.sqlite3_stmt statement) => _statement = statement;

    private bool _disposed;

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _statement.Dispose();
        }
        _disposed = true;
        base.Dispose(disposing);
    }

    public override int Depth => throw new NotImplementedException();

    public override int FieldCount => SQLitePCL.raw.sqlite3_column_count(_statement);

    public override bool HasRows => throw new NotImplementedException();

    public override bool IsClosed => throw new NotImplementedException();

    public override int RecordsAffected => throw new NotImplementedException();

    public override object this[string name] => throw new NotImplementedException();

    public override object this[int ordinal] => throw new NotImplementedException();

    public override string GetDataTypeName(int ordinal) => SQLitePCL.raw.sqlite3_column_type(_statement, ordinal) switch
    {
        SQLitePCL.raw.SQLITE_INTEGER => "int",
        SQLitePCL.raw.SQLITE_FLOAT => "float",
        SQLitePCL.raw.SQLITE_TEXT => "text",
        SQLitePCL.raw.SQLITE_BLOB => "blob",
        SQLitePCL.raw.SQLITE_NULL => "null",
        _ => "unknown",
    };

    public override IEnumerator GetEnumerator() => throw new NotImplementedException();

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
    public override Type GetFieldType(int ordinal) => throw new NotImplementedException();

    public override string GetName(int ordinal) => throw new NotImplementedException();

    public override int GetOrdinal(string name) => throw new NotImplementedException();

    public override bool GetBoolean(int ordinal)
    {
        var val = SQLitePCL.raw.sqlite3_column_int(_statement, ordinal);
        return val > 0;
    }

    public override byte GetByte(int ordinal) => throw new NotImplementedException();

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var a = SQLitePCL.raw.sqlite3_column_blob(_statement, ordinal);
        if (a.IsEmpty)
        {
            return 0;
        }
        var span = new Span<byte>(buffer);
        a.CopyTo(span);
        return a.Length;
    }

    public override char GetChar(int ordinal) => throw new NotImplementedException();

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();

    public override DateTime GetDateTime(int ordinal) => throw new NotImplementedException();

    public override decimal GetDecimal(int ordinal) => throw new NotImplementedException();

    public override double GetDouble(int ordinal) => SQLitePCL.raw.sqlite3_column_double(_statement, ordinal);

    public override float GetFloat(int ordinal) => throw new NotImplementedException();

    public override Guid GetGuid(int ordinal) => throw new NotImplementedException();

    public override short GetInt16(int ordinal) => throw new NotImplementedException();

    public override int GetInt32(int ordinal) => SQLitePCL.raw.sqlite3_column_int(_statement, ordinal);

    public override long GetInt64(int ordinal) => SQLitePCL.raw.sqlite3_column_int64(_statement, ordinal);

    public override string GetString(int ordinal) => SQLitePCL.raw.sqlite3_column_text(_statement, ordinal).utf8_to_string();

    public override object GetValue(int ordinal) => throw new NotImplementedException();

    public override int GetValues(object[] values) => throw new NotImplementedException();

    public override bool IsDBNull(int ordinal)
    {
        var type = SQLitePCL.raw.sqlite3_column_type(_statement, ordinal);
        return type == SQLitePCL.raw.SQLITE_NULL;
    }

    public override bool NextResult() => throw new NotImplementedException();

    private static bool IsBusy(int rc)
        => rc is SQLitePCL.raw.SQLITE_LOCKED
            or SQLitePCL.raw.SQLITE_BUSY
            or SQLitePCL.raw.SQLITE_LOCKED_SHAREDCACHE;

    public override bool Read()
    {
        int res;
        int tries = 0;
        while (IsBusy(res = SQLitePCL.raw.sqlite3_step(_statement)))
        {
            if (tries > 20)
            {
                break;
            }
            SQLitePCL.raw.sqlite3_reset(_statement);

            Thread.Sleep(150);
            tries++;
        }

        if (res == SQLitePCL.raw.SQLITE_ROW)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
