﻿using System.Data.SQLite;
using Dapper;

namespace DotNetFlix.Data;

internal static class Schema
{
    const long Version = 1;

    public static void EnsureSchema(this SQLiteConnection sql)
    {
        var version = sql.ExecuteScalar<long>("PRAGMA user_version");

        for (var i = version; i < Version; i++)
        {
            Migrate(sql, i);
        }
    }

    private static void Migrate(SQLiteConnection sql, long i)
    {
        switch (i)
        {
            case 0:
                Migrate0To1(sql);
                break;
        }
    }

    private static void Migrate0To1(SQLiteConnection sql) => sql.Execute($@"
PRAGMA journal_mode = WAL;
PRAGMA user_version = 1;

CREATE TABLE {SettingsTable.TableName} (
    [{nameof(SettingsTable.Key)}] TEXT,
    [{nameof(SettingsTable.Value)}] TEXT);

CREATE TABLE {SessionsTable.TableName} (
    [{nameof(SessionsTable.Id)}] INTEGER PRIMARY KEY,
    [{nameof(SessionsTable.Token)}] TEXT,
    [{nameof(SessionsTable.Page)}] TEXT,
    [{nameof(SessionsTable.CreatedUnixTimestamp)}] INTEGER);

CREATE TABLE {SessionDataTable.TableName} (
    [{nameof(SessionDataTable.Id)}] INTEGER PRIMARY KEY,
    [{nameof(SessionDataTable.SessionId)}] INTEGER,
    [{nameof(SessionDataTable.Key)}] TEXT,
    [{nameof(SessionDataTable.Value)}] TEXT);

CREATE TABLE {FileUploadsTable.TableName} (
    [{nameof(FileUploadsTable.Id)}] INTEGER PRIMARY KEY,
    [{nameof(FileUploadsTable.SessionId)}] INTEGER,
    [{nameof(FileUploadsTable.Name)}] TEXT,
    [{nameof(FileUploadsTable.Size)}] INTEGER,
    [{nameof(FileUploadsTable.CreatedUnixTimestamp)}] INTEGER,
    [{nameof(FileUploadsTable.UploadCompletedUnixTimestamp)}] INTEGER);
");
}
