using System.Data.SQLite;
using System.Text;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Pages;

internal class Player : Page
{
    const string HomeAction = "Home";

    public override async Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var currentMediaIdString = sql.GetSessionData(sessionId, SessionDataKeys.MediaId);
        var currentMediaId = long.Parse(currentMediaIdString);
        var media = sql.GetMedia(currentMediaId);

        if (context.Request.Path.StartsWithSegments("/video"))
        {
            var mediaStream = new MediaStream(sql, media.Id);
            await MediaExtensions.ServeRangeVideoContent(context, mediaStream, media.Size, Constants.VideoContentType);
            return;
        }

        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(HtmlTemplate(Html(media), Css(), Js())), context.RequestAborted);
    }

    public override async Task Post(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var form = await context.Request.ReadFormAsync();

        switch (form[Action])
        {
            case HomeAction:
                sql.SetSessionPage(sessionId, nameof(Home));
                await Instance(nameof(Home)).Get(context, sql, sessionId);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    string Html(Media media) => $@"

<div class='container'>

 <form action='/' method='POST' enctype='multipart/data'>
    <button type='submit' name='{Action}' value='{HomeAction}'>Home</button>
</form>

<h1>{media.Title}</h1>

{(media.IsPending ? "Media unavailable - retry in a few seconds." : $@"
    <video src='/video'></video>
")}

</div>
";

    string Css() => $@"";

    string Js() => $@"";
}
