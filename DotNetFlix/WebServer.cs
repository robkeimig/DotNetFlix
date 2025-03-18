using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetFlix;

internal class WebServer
{
    public WebServer()
    {
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
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(Html));
    }

    const string Html = @"
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset=""utf-8"" />
    <title>DotNetFlix</title>
    
    <style>
        body, html {
            background-color:#000;
            margin: 0;
            height: 100%;
            display: flex;
            align-items: center;
            justify-content: center;
        }
    </style>

    <script>
       
    </script>
</head>

<body>
    <p>Hello!</p>
</body>

</html>
";
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
            app.Use((HttpContext c, Func<Task> _) =>
                server.ProcessHttpContext(c));
}
