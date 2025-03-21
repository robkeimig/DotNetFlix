﻿using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

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

    public abstract Task Get(HttpContext context, SqliteConnection sql, long sessionId);

    public abstract Task Post(HttpContext context, SqliteConnection sql, long sessionId);    

    public static string HtmlTemplate(string html, string css, string js) => $@"
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset=""utf-8"" />
    <title>DotNetFlix</title>
    
    <style>
        body, html {{
            background-color:#000;
            margin: 0;
            height: 100%;
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
