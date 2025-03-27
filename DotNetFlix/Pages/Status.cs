﻿using System.Data.SQLite;
using System.Text;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Pages;

internal class Status : Page
{
    const string HomeAction = "Home";
    const string StatusTableBodyElement = "StatusTableBody";

    public override async Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);
        var pendingMedia = sql.GetPendingMedia();
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(HtmlTemplate(Html(pendingMedia), Css(), Js())), context.RequestAborted);
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

    
    string Html(List<Media> pendingMedia) => $@"
<div class='container'>
     <form action='/' method='POST' enctype='multipart/data'>
        <button type='submit' name='{Action}' value='{HomeAction}'>Home</button>
    </form>

    <h1>Status</h1>
    <h3>Media</h3>
    <table>
        <thead>
            <tr>    
                <th>Title</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody id='{StatusTableBodyElement}'>
            <tr colspan=2>
                <td>Loading...</td>
            </tr>
        </tbody>
    </table>
</div>
";

    string Css() => $@"";

    string Js() => $@"//TODO: Periodically refresh StatusTableBodyElement with all Media items having IsPending == true.";
}
