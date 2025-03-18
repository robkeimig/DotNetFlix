using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using DotNetFlix.Data;
using DotNetFlix.Pages;

namespace DotNetFlix;

internal class WebServer
{
    readonly SqliteConnection Sql;

    public WebServer(SqliteConnection sql)
    {
        Sql = sql;

        WebHost.CreateDefaultBuilder()
            .UseKestrel(k =>
            {
                k.ListenAnyIP(8001);
            })
            .UseStartup<WebStartup>()
            .ConfigureLogging(cl =>
            {
                cl.ClearProviders();
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton(this);
            })
            .Build()
            .RunAsync();
    }

    internal async Task ProcessHttpContext(HttpContext context)
    {
        var cookie = context.GetSessionToken();
        var session = Sql.GetSession(cookie);

        if (session == null)
        {
            session = Sql.CreateSession();
            context.SetSessionToken(session.Token);
            session.Page = nameof(Home);
            Sql.SetSessionResource(session.Id, session.Page);
        }

        var page = Page.Instance(session.Page);

        if (context.Request.Method.Equals("post", StringComparison.CurrentCultureIgnoreCase))
        {
            var form = await context.Request.ReadFormAsync();
            var action = form.FirstOrDefault(x => x.Key == Page.Action).Value.ToString();
            await page.Post(Sql, session.Id, form);
            session = Sql.GetSession(session.Id);
            page = Page.Instance(session.Page);
        }

        var content = await page.Get(Sql, session.Id);
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(content), context.RequestAborted);
    }
}

internal class WebStartup
{
    public void Configure(IApplicationBuilder app, WebServer server)
    {
        app.UseWebSockets();
        app.UseWebServer(server);
    }
}

internal static class WebExtensions
{
    internal static IApplicationBuilder UseWebServer(
        this IApplicationBuilder app,
        WebServer server) =>
            app.Use(async (HttpContext c, Func<Task> _) =>
            {
                try
                {
                    await server.ProcessHttpContext(c);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            });
}
