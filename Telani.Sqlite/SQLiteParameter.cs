using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Telani.Sqlite;

/// <summary>
/// A SQLite parameter to add dynamic values to sql-queries.
/// </summary>
public sealed class SQLiteParameter : DbParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="name">The parameter value</param>
    public SQLiteParameter(string v, string? name)
    {
        ParameterName = v;
        DbType = DbType.String;
        Value = name;
        IsNullable = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="n">The parameter value</param>
    public SQLiteParameter(string v, int n)
    {
        ParameterName = v;
        DbType = DbType.Int32;
        Value = n;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="n">The parameter value</param>
    public SQLiteParameter(string v, long n)
    {
        ParameterName = v;
        DbType = DbType.Int64;
        Value = n;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="n">The parameter value</param>
    public SQLiteParameter(string v, long? n)
    {
        ParameterName = v;
        DbType = DbType.Int64; // Is null a problem?
        Value = n;
        IsNullable = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="value">The parameter value</param>
    public SQLiteParameter(string v, bool value)
    {
        ParameterName = v;
        DbType = DbType.Boolean;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="value">The parameter value</param>
    public SQLiteParameter(string v, double value)
    {
        ParameterName = v;
        DbType = DbType.Double;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <remarks>
    /// Value is set by caller
    /// </remarks>
    /// <param name="v">The name of the parameter</param>
    /// <param name="value">the type of value</param>
    /// <param name="length">the length of the value</param>
    public SQLiteParameter(string v, DbType value, int length)
    {
        ParameterName = v;
        DbType = value;
        Size = length;

        // Value is set by caller
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="date">The parameter value</param>
    public SQLiteParameter(string v, DateTime date)
    {
        ParameterName = v;
        DbType = DbType.DateTime;
        Value = date;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteParameter"/> class.
    /// </summary>
    /// <param name="v">The parameter name</param>
    /// <param name="date">The parameter value</param>
    public SQLiteParameter(string v, DateTime? date)
    {
        ParameterName = v;
        DbType = DbType.DateTime;
        Value = date;
        IsNullable = true;
    }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override DbType DbType { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override ParameterDirection Direction { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override bool IsNullable { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override int Size { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    [AllowNull]
    public override string SourceColumn { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override bool SourceColumnNullMapping { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override object? Value { get; set; }

    /// <inheritdoc/>
    /// <remarks>Not implemented</remarks>
    public override void ResetDbType() => DbType = DbType.AnsiString;
}
