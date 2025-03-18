using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

internal class Home : Page
{
    public const string UploadAction = "Upload";

    public override bool IsDefault => true;

    public override async Task<string> Get(SqliteConnection sql, long sessionId)
    {
        var movies = sql.GetMovies(sessionId);

        return HtmlTemplate(Html(movies), Css(), Js());
    }

    public override Task Post(SqliteConnection sql, long sessionId, IFormCollection form)
    {
        var action = form[Action];

        switch (action)
        {
            case UploadAction:
                throw new NotImplementedException("TODO: Next work area.");
                break;
            default:
                throw new NotImplementedException();
        }
    }

    string Html(List<Movie> movies) => $@"
<div class='container' />
    <h1>Movies</h1>
    <form action='/' method='POST' enctype='multipart/data'>
        <button type='submit' name='{Action}' value='{UploadAction}'>Upload Movie</button>
    </form>
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
