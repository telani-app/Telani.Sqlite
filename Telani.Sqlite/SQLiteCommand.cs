using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Telani.Sqlite;

namespace Telani.Sqlite;

/// <summary>
/// Wrapper around a SQLite statement
/// </summary>
internal sealed class SQLiteCommand
{
    private readonly SQLitePCL.sqlite3 conn;

    public SQLiteCommand(SQLitePCL.sqlite3 conn) => this.conn = conn;

    public string? CommandText { get; set; }

    public List<DbParameter> Parameters { get; } = [];

    public Task ExecuteNonQueryAsync()
    {
        ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private static SQLiteException CreateException(int returnCode, string description, string? commandText, SQLitePCL.sqlite3 conn)
    {
        var errorCode = SQLitePCL.raw.sqlite3_errcode(conn);
        Debug.Assert(errorCode == returnCode);
        var extendedErrorCode = SQLitePCL.raw.sqlite3_extended_errcode(conn);
        var errorMsg_utf8 = SQLitePCL.raw.sqlite3_errmsg(conn);
        var errorMsg = errorMsg_utf8.utf8_to_string();
        var except = returnCode switch
        {
            SQLitePCL.raw.SQLITE_CORRUPT => new SQLiteException(SQLiteExceptionSubType.SQLITE_CORRUPT, "Error: Database corrupt. Msg: " + errorMsg + " ExtendedError: " + extendedErrorCode),
            SQLitePCL.raw.SQLITE_CONSTRAINT => new SQLiteException(SQLiteExceptionSubType.SQLITE_CONSTRAINT, "Error: Database constrained violation. Msg: " + errorMsg + " ExtendedError: " + extendedErrorCode),
            _ => new SQLiteException(SQLiteExceptionSubType.SQLITE_GENERAL, description + ", ResultCode: " + returnCode + " Msg: " + errorMsg + " ExtendedError: " + extendedErrorCode),
        };
        except.ExtendedErrorCode = extendedErrorCode;
        except.SqlLiteErrorCode = returnCode;
        except.CommandText = commandText;
        return except;
    }

    private static bool IsBusy(int rc)
        => rc is SQLitePCL.raw.SQLITE_LOCKED or SQLitePCL.raw.SQLITE_BUSY or SQLitePCL.raw.SQLITE_LOCKED_SHAREDCACHE;

    private static SQLitePCL.sqlite3_stmt PrepareStatement(string? commandText, SQLitePCL.sqlite3 conn)
    {
        int result;
        int tries = 0;
        SQLitePCL.sqlite3_stmt statement;
        while (IsBusy(result = SQLitePCL.raw.sqlite3_prepare_v2(conn, commandText, out statement)))
        {
            if (tries > 20)
            {
                break;
            }
            tries++;

            Thread.Sleep(150);
        }
        if (result != SQLitePCL.raw.SQLITE_OK)
        {
            throw CreateException(result, "Error during statement preparation", commandText, conn);
        }
        return statement;
    }

    public void ExecuteNonQuery()
    {
        SQLitePCL.sqlite3_stmt? statement = null;
        try
        {
            statement = PrepareStatement(CommandText, conn);
            if (Parameters is not null)
            {
                ApplyBind(statement);
            }
            int status;
            int tries = 0;
            while (IsBusy(status = SQLitePCL.raw.sqlite3_step(statement)))
            {
                // From SQLite Docs: "If the statement is a COMMIT [..], then you can retry the statement."
                if (CommandText?.ToUpperInvariant() is not "COMMIT;" and not "COMMIT")
                {
                    break;
                }
                if (tries > 20)
                {
                    break;
                }
                tries++;
                Task.Delay(150);
            }

            if (status != SQLitePCL.raw.SQLITE_DONE)
            {
                throw CreateException(status, "Error during database step", CommandText, conn);
            }
        }
        finally
        {
            statement?.Dispose();
        }
    }

    /// <summary>
    /// Execute the command to get a reader.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public Task<DbDataReader> ExecuteReaderAsync()
    {
        var statement = PrepareStatement(CommandText, conn);
        ApplyBind(statement);

        return Task.FromResult<DbDataReader>(new SQLiteReader(statement));
    }

    private void ApplyBind(SQLitePCL.sqlite3_stmt statement)
    {
#if DEBUG
        // This does not work if the "?NNN" form of parameter is used, which we do not use.
        var number_of_parameters = SQLitePCL.raw.sqlite3_bind_parameter_count(statement);
        Debug.Assert(number_of_parameters <= Parameters.Count, "There where not enough parameters provided for all parameters in this query");
#endif
        if (Parameters is not null && Parameters.Count > 0)
        {
            foreach (var param in Parameters)
            {
                int result;

                var ordinal = SQLitePCL.raw.sqlite3_bind_parameter_index(statement, param.ParameterName);
                if (ordinal != 0)
                {
                    if (param.Value is null)
                    {
                        result = SQLitePCL.raw.sqlite3_bind_null(statement, ordinal);
                    }
                    else
                    {
                        result = param.DbType switch
                        {
                            DbType.Int32 => SQLitePCL.raw.sqlite3_bind_int(statement, ordinal, (int)param.Value),
                            DbType.Int64 => SQLitePCL.raw.sqlite3_bind_int64(statement, ordinal, (long)param.Value),
                            DbType.String => SQLitePCL.raw.sqlite3_bind_text(statement, ordinal, (string)param.Value),
                            DbType.Boolean => SQLitePCL.raw.sqlite3_bind_int(statement, ordinal, (bool)param.Value ? 1 : 0),
                            DbType.Binary => SQLitePCL.raw.sqlite3_bind_blob(statement, ordinal, new ReadOnlySpan<byte>((byte[])param.Value, 0, param.Size)),
                            DbType.DateTime => SQLitePCL.raw.sqlite3_bind_int64(statement, ordinal, ((DateTime)param.Value).Ticks),
                            DbType.Double => SQLitePCL.raw.sqlite3_bind_double(statement, ordinal, (double)param.Value),
                            _ => throw new NotImplementedException(),
                        };
                    }
                    if (result != SQLitePCL.raw.SQLITE_OK)
                    {
                        throw CreateException(result, "Error binding parameter", CommandText, conn);
                    }
                }
            }
        }
    }
}
