using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix;

internal abstract class Resource
{
    static readonly List<Resource> Resources;

    static Resource()
    {
        Resources = new List<Resource>();

        var resourceTypes = Assembly.GetExecutingAssembly()
               .GetTypes()
               .Where(t => t.IsSubclassOf(typeof(Resource)) && !t.IsAbstract);

        foreach (var t in resourceTypes)
        {
            Resources.Add((Resource)Activator.CreateInstance(t));
        }

        if (!Resources.Any(x => x.IsDefault))
        {
            throw new Exception($"Cannot proceed without a default Resource.");
        }
    }

    public static Task ProcessRequest(HttpContext httpContext)
    {
        foreach(var resource in Resources)
        {
            if (resource.IsMatch(httpContext))
            {
                return resource.ProcessRequestInternal(httpContext);
            }
        }

        return Resources.First(x => x.IsDefault).ProcessRequestInternal(httpContext);
    }

    internal virtual bool IsDefault => false;
    internal abstract bool IsMatch(HttpContext httpContext);
    internal abstract Task ProcessRequestInternal(HttpContext httpContext);
}