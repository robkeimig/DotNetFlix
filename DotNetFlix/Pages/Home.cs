using System.Data.SQLite;
using System.Text;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Pages;

internal class Home : Page
{
    public const string UploadAction = "Upload";
    public const string SettingsAction = "Settings";
    public const string MediaAction = "Media";
    public const string StatusAction = "Status";

    public override bool IsDefault => true;

    public override async Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        if (context.Request.Path.StartsWithSegments("/watch", out var remainingPath))
        {
            if (long.TryParse(remainingPath.ToString().Trim('/').Trim('\\'), out var mediaId))
            {
                sql.SetSessionData(sessionId, SessionDataKeys.MediaId,  mediaId.ToString());
                sql.SetSessionPage(sessionId, nameof(Player));
                await Instance(nameof(Player)).Get(context, sql, sessionId);
                return;
            }
        }

        var session = sql.GetSession(sessionId);
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(View(sql, sessionId)), context.RequestAborted);
    }

    public override async Task Post(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var form = await context.Request.ReadFormAsync();

        switch (form[Action])
        {
            case UploadAction:
                sql.SetSessionPage(sessionId, nameof(Upload));
                await Instance(nameof(Upload)).Get(context, sql, sessionId);
                break;
            case SettingsAction:
                sql.SetSessionPage(sessionId, nameof(Settings));
                await Instance(nameof(Settings)).Get(context, sql, sessionId);
                break;
            case StatusAction:
                sql.SetSessionPage(sessionId, nameof(Status));
                await Instance(nameof(Status)).Get(context, sql, sessionId);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    string View(SQLiteConnection sql, long sessionId)
    {
        var media = sql.GetMedia();
        return HtmlTemplate(Html(media), Css(), Js());
    }

    string Html(List<Media> media) => $@"
<div class='container' />
    <form action='/' method='POST' enctype='multipart/data'>
        <button type='submit' name='{Action}' value='{UploadAction}'>Upload Media</button>
        <button type='submit' name='{Action}' value='{SettingsAction}'>Settings</button>
        <button type='submit' name='{Action}' value='{StatusAction}'>Status</button>
    </form>
    <h1>Media</h1>
    <table>
        <tr>
            <th>Title</th>
        </tr>
        {string.Join('\n',media.Select(m => $@"
            <tr>
                <td><a href='Watch/{m.Id}'>{m.Title}</a></td>
            </tr>
        "))}
    </table>
</div>
";

    string Css() =>@$"
a {{
    color: #FFF;
}}
.container {{
}}

.container p {{
    margin-top:0;
}}
";

    string Js() => $@"
";
}
