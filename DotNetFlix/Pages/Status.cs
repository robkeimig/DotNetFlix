using System.Data.SQLite;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Pages;

internal class Status : Page
{
    public override Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        //TODO: Render a periodically-refreshing view of all Media items with IsPending == true.
        throw new NotImplementedException();
    }

    public override Task Post(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        throw new NotImplementedException();
    }
}
