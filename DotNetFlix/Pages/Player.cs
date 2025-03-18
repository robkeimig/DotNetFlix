using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

public class Player : Page
{
    public override bool IsDefault => true;

    public override Task<string> Get(SqliteConnection sql, long sessionId)
    {
        return Task.FromResult(HtmlTemplate(Html, Css, Js));
    }

    public override Task Post(SqliteConnection sql, long sessionId, IFormCollection form)
    {
        throw new NotImplementedException();
    }

    string Html = $@"
<p>Inner html</p>
";

    string Css = @"";

    string Js = @"";

}