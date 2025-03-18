using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix;

public abstract class Page
{
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

    public abstract Task<Content> Get(SqliteConnection sql);

    public abstract Task Post(SqliteConnection sql, IFormCollection form);
}
