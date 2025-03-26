using System.Data.SQLite;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix;

public abstract class Page
{
    public const string Action = "Action";
    static readonly Dictionary<string, Page> PageMap;
    static Page()
    {
        PageMap = new Dictionary<string, Page>();
        var pageTypes = Assembly.GetExecutingAssembly()
               .GetTypes()
               .Where(t => t.IsSubclassOf(typeof(Page)) && !t.IsAbstract);

        foreach (var t in pageTypes)
        {
            var i = (Page)Activator.CreateInstance(t);
            PageMap[i.GetType().Name] = i;
        }
    }

    public static Page Instance(string name) => PageMap[name];

    public static Page Default => PageMap.First(x => x.Value.IsDefault).Value;

    public virtual bool IsDefault { get; }

    public abstract Task Get(HttpContext context, SQLiteConnection sql, long sessionId);

    public abstract Task Post(HttpContext context, SQLiteConnection sql, long sessionId);    

    public static string HtmlTemplate(string html, string css, string js) => $@"
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset=""utf-8"" />
    <title>DotNetFlix</title>
    
    <style>
        body, html {{
            margin: 0;
            height: 100%;
            background-color:#000;
            color:#d0d0d0;
        }}

        {css}
    </style>
    
</head>

<body>
    {html}
    <script>
        {js}      
    </script>
</body>
</html>
";
}
