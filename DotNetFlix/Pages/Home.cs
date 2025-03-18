using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

internal class Home : Page
{
    public override bool IsDefault => true;

    public override async Task<string> Get(SqliteConnection sql, long sessionId)
    {
        return "Hello!";
    }

    public override Task Post(SqliteConnection sql, long sessionId, IFormCollection form)
    {
        throw new NotImplementedException();
    }
}
