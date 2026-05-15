using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Telani.Sqlite;

/// <summary>
/// This class contains methods for analyzing a database file without needing to open a connection or load the full database.
/// </summary>
public static class DatabaseFileAnalyzer
{
    /// <summary>
    /// Read the user version from the header of a SQLite database file. This is a simple way to determine
    /// the schema version of the database without needing to open a connection or load the full database.
    /// </summary>
    /// <param name="s">The stream of the database file</param>
    /// <returns>The user version of the database</returns>
    public static int GetDBVersionFromStream(Stream s)
    {
        if (s.Length > 64)
        {
            s.Seek(60, SeekOrigin.Begin);

            var userVersion = new byte[4];
            s.ReadExactly(userVersion);
            return (int)BinaryPrimitives.ReadUInt32BigEndian(userVersion);
        }
        else
        {
            return 0;
        }
    }
}
