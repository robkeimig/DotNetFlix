using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Data;

public class Session
{
    public const string TokenCookieName = "Token";

    public long Id;
    public string Token;
    public string Page;
    public DateTime CreatedUtc;
}

public class SessionsTable
{
    public const string TableName = "Sessions";
    public long Id { get; set; }
    public string Token { get; set; }
    public string Page { get; set; }
    public long CreatedUnixTimestamp { get; set; }
}

public static class SessionExtensions
{
    internal static SessionsTable MapRow(this Session session) => new SessionsTable
    {
        Id = session.Id,
        CreatedUnixTimestamp = new DateTimeOffset(session.CreatedUtc).ToUnixTimeSeconds(),
        Page = session.Page,
        Token = session.Token,
    };

    internal static Session MapItem(this SessionsTable session) => new Session
    {
        Id = session.Id,
        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(session.CreatedUnixTimestamp).UtcDateTime,
        Token = session.Token,
        Page = session.Page,
    };

    internal static string GetSessionToken(this HttpContext context)
    {
        return context.Request.Cookies[Session.TokenCookieName] ?? string.Empty;
    }

    internal static void SetSessionToken(this HttpContext context, string token)
    {
        context.Response.Cookies.Append(Session.TokenCookieName, token, new CookieOptions
        {
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365 * 5),
            SameSite = SameSiteMode.Strict,
            HttpOnly = true
        });
    }

    internal static void ClearSessionToken(this HttpContext context)
    {
        context.Response.Cookies.Append(Session.TokenCookieName, string.Empty, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        });
    }

    internal static Session? GetSession(this SqliteConnection sql, string token)
    {
        var sessionRow = sql.QueryFirstOrDefault<SessionsTable>($@"
            SELECT * 
            FROM {SessionsTable.TableName} 
            WHERE       [{nameof(SessionsTable.Token)}] = @{nameof(SessionsTable.Token)}",
            new
            {
                Token = token
            });

        if (sessionRow == null) { return null; }
        var session = sessionRow.MapItem();
        return session;
    }

    public static Session GetSession(this SqliteConnection sql, long id)
    {
        var sessionRow = sql.QueryFirst<SessionsTable>($@"
            SELECT * 
            FROM {SessionsTable.TableName} 
            WHERE   [{nameof(SessionsTable.Id)}] = @{nameof(SessionsTable.Id)}",
            new
            {
                Id = id
            });

        var session = sessionRow.MapItem();
        return session;
    }


    public static Session CreateSession(this SqliteConnection sql)
    {
        var session = new Session
        {
            Token = Cryptography.GenerateTokenString(),
            CreatedUtc = DateTime.UtcNow,
        };

        session.Id = sql.ExecuteScalar<long>($@"
            INSERT INTO {SessionsTable.TableName}  (
                [{nameof(SessionsTable.CreatedUnixTimestamp)}],
                [{nameof(SessionsTable.Token)}]) 
            VALUES (
                @{nameof(SessionsTable.CreatedUnixTimestamp)},
                @{nameof(SessionsTable.Token)})
            RETURNING [{nameof(SessionsTable.Id)}]",
            session.MapRow());

        return session;
    }

    public static void SetSessionPage(this SqliteConnection sql, long sessionId, string page)
    {
        sql.Execute($@"
            UPDATE {SessionsTable.TableName}
            SET     [{nameof(SessionsTable.Page)}] = @{nameof(SessionsTable.Page)}
            WHERE   [{nameof(SessionsTable.Id)}] = @{nameof(SessionsTable.Id)}", new
        {
            Id = sessionId,
            Page = page,
        });
    }
}