using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

internal class Admin : Page
{
    public override Task Get(HttpContext context, SqliteConnection sql, long sessionId)
    {
        //TODO: Display table for configuration of the various Settings.
        throw new NotImplementedException();
    }

    public override Task Post(HttpContext context, SqliteConnection sql, long sessionId)
    {
        //TODO: Handle setting updates and Home navigation events.
        throw new NotImplementedException();
    }
}
