using System.Data.SQLite;
using Dapper;

namespace DotNetFlix.Data;

public class SessionData
{
    public long Id;
    public long SessionId;
    public string Key;
    public string Value;
}

public class SessionDataTable
{
    public const string TableName = "SessionData";

    public long Id { get; set; }
    public long SessionId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}

public static class SessionDataDataExtensions
{
    public static string? GetSessionData(this SQLiteConnection sql, long sessionId, string key)
    {
        return sql.ExecuteScalar<string>($@"
            SELECT [{nameof(SessionDataTable.Value)}]
            FROM [{SessionDataTable.TableName}]
            WHERE [{nameof(SessionDataTable.SessionId)}] = @{nameof(SessionDataTable.SessionId)}
            AND     [{nameof(SessionDataTable.Key)}] = @{nameof(SessionDataTable.Key)}", new
        {
            SessionId = sessionId,
            Key = key
        });
    }

    public static void ClearSessionData(this SQLiteConnection sql, long sessionId)
    {
        sql.Execute($@"DELETE FROM {SessionDataTable.TableName} WHERE [{nameof(SessionDataTable.SessionId)}] = @{nameof(SessionDataTable.SessionId)}", new
        {
            SessionId = sessionId
        });
    }

    public static void ClearSessionData(this SQLiteConnection sql, long sessionId, string key)
    {
        sql.Execute($@"
            DELETE 
            FROM {SessionDataTable.TableName} 
            WHERE   [{nameof(SessionDataTable.SessionId)}] = @{nameof(SessionDataTable.SessionId)}
            AND     [{nameof(SessionDataTable.Key)}] = @{nameof(SessionDataTable.Key)}", new
        {
            SessionId = sessionId,
            Key = key
        });
    }

    public static void SetSessionData(this SQLiteConnection sql, long sessionId, string key, string value)
    {
        var existing = sql.QueryFirstOrDefault<SessionDataTable>($@"
            SELECT * FROM {SessionDataTable.TableName}
            WHERE   [{nameof(SessionDataTable.SessionId)}] = @{nameof(SessionDataTable.SessionId)}
            AND     [{nameof(SessionDataTable.Key)}] = @{nameof(SessionDataTable.Key)}", new
        {
            SessionId = sessionId,
            Key = key
        });

        if (existing == null)
        {
            sql.Execute($@"
            INSERT INTO [{SessionDataTable.TableName}]
            (
                [{nameof(SessionDataTable.SessionId)}],
                [{nameof(SessionDataTable.Key)}]
            )
            VALUES 
            (
                @{nameof(SessionDataTable.SessionId)},
                @{nameof(SessionDataTable.Key)}
            )", new
            {
                SessionId = sessionId,
                Key = key,
            });
        }

        sql.Execute($@"UPDATE [{SessionDataTable.TableName}]
            SET     [{nameof(SessionDataTable.Value)}] = @{nameof(SessionDataTable.Value)}
            WHERE   [{nameof(SessionDataTable.SessionId)}] = @{nameof(SessionDataTable.SessionId)}
            AND     [{nameof(SessionDataTable.Key)}] = @{nameof(SessionDataTable.Key)}", new
        {
            SessionId = sessionId,
            Key = key,
            Value = value
        });
    }

    public static SessionData Map(SessionDataTable sessionData) => new SessionData
    {
        Id = sessionData.Id,
        SessionId = sessionData.SessionId,
        Key = sessionData.Key,
        Value = sessionData.Value,
    };

    public static SessionDataTable Map(SessionData sessionData) => new SessionDataTable
    {
        Id = sessionData.Id,
        SessionId = sessionData.SessionId,
        Key = sessionData.Key,
        Value = sessionData.Value,
    };
}
