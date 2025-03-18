using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

public class Upload : Page
{
    public override Task<Content> Get(SqliteConnection sql)
    {
        throw new NotImplementedException();
    }

    public override Task Post(SqliteConnection sql, IFormCollection form)
    {
        throw new NotImplementedException();
    }
}
