using Dapper;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Data;

internal static class Schema
{
    const long Version = 1;

    public static void EnsureSchema(this SqliteConnection sql)
    {
        var version = sql.ExecuteScalar<long>("PRAGMA user_version");

        for (var i = version; i < Version; i++)
        {
            Migrate(sql, i);
        }
    }

    private static void Migrate(SqliteConnection sql, long i)
    {
        switch (i)
        {
            case 0:
                Migrate0To1(sql);
                break;
        }
    }

    private static void Migrate0To1(SqliteConnection sql) => sql.Execute($@"
PRAGMA journal_mode = WAL;
PRAGMA user_version = 1;

CREATE TABLE {SettingsTable.TableName} (
    [{nameof(SettingsTable.Key)}] TEXT,
    [{nameof(SettingsTable.Value)}] TEXT);

CREATE TABLE {SessionsTable.TableName} (
    [{nameof(SessionsTable.Id)}] INTEGER PRIMARY KEY,
    [{nameof(SessionsTable.Token)}] TEXT,
    [{nameof(SessionsTable.Page)}] TEXT,
    [{nameof(SessionsTable.CreatedUnixTimestamp)}] INTEGER);");
}
