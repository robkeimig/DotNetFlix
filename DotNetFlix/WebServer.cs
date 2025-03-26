using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Features;
using System.Data.SQLite;
using DotNetFlix.Data;
using DotNetFlix.Pages;

namespace DotNetFlix;

internal class WebServer
{
    readonly SQLiteConnection Sql;

    public WebServer(SQLiteConnection sql)
    {
        Sql = sql;

        WebHost.CreateDefaultBuilder()
            .UseKestrel(k =>
            {
                k.ListenAnyIP(80);
            })
            .UseStartup<WebStartup>()
            .ConfigureLogging(cl =>
            {
                cl.ClearProviders();
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton(this);
                s.Configure<FormOptions>(options =>
                {
                    options.MultipartBodyLengthLimit = Constants.MaximumUploadSize;
                });
            })
            .ConfigureKestrel(s =>
            {
                s.Limits.MaxRequestBodySize = Constants.MaximumUploadSize;
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
            Sql.SetSessionPage(session.Id, session.Page);
        }

        var page = Page.Instance(session.Page);
        
        if (context.Request.Method.Equals("post", StringComparison.CurrentCultureIgnoreCase))
        {
            await page.Post(context, Sql, session.Id);
        }
        else
        {
            await page.Get(context, Sql, session.Id);
        }
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
    internal static IApplicationBuilder UseWebServer(this IApplicationBuilder app, WebServer server) =>
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
