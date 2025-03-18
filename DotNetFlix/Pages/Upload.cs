using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

public class Upload : Page
{
    public override Task<string> Get(SqliteConnection sql, long sessionId)
    {
        throw new NotImplementedException();
    }

    public override Task Post(SqliteConnection sql, long sessionId, IFormCollection form)
    {
        throw new NotImplementedException();
    }
}
