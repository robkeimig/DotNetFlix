using System.Text;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

internal class Home : Page
{
    public const string UploadAction = "Upload";

    public override bool IsDefault => true;

    public override async Task ProcessHttpContext(HttpContext context, SqliteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);

        if (context.Request.Method.Equals("post", StringComparison.CurrentCultureIgnoreCase))
        {
            var form = await context.Request.ReadFormAsync();
            
            switch (form[Action])
            {
                case UploadAction:
                    sql.SetSessionPage(sessionId, nameof(Upload));
                    await Instance(nameof(Upload)).ProcessHttpContext(context, sql, session.Id);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        else
        {
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(View(sql, sessionId)), context.RequestAborted);
        }
    }

    string View(SqliteConnection sql, long sessionId)
    {
        var movies = sql.GetMovies(sessionId);
        return HtmlTemplate(Html(movies), Css(), Js());
    }

    string Html(List<Movie> movies) => $@"
<div class='container' />
    <form action='/' method='POST' enctype='multipart/data'>
        <button type='submit' name='{Action}' value='{UploadAction}'>Upload Media</button>
    </form>
    <h1>Movies</h1>
    <table>
        <tr>
            <th>Title</th>
            <th>Year</th>
            <th>Genre</th>
            <th></th>
        </tr>
        {string.Join('\n',movies.Select(movie => $@"
            <tr>
                <td>{movie.Title}</td>
                <td>{movie.Genre}</td>
                <td>{movie.Year}</td>
                <td><a href='/...'>Watch Now</td>
            </tr>
        "))}
    </table>
</div>
";

    string Css() =>@$"
.container {{
    background-color:#ff00ff;
}}

.container p {{
    margin-top:0;
}}
";

    string Js() => $@"
//...
";
}
