using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Resources
{
    //Typical streaming content browser experience.
    internal class Home : Resource
    {
        internal override bool IsMatch(HttpContext httpContext)
        {
            if (httpContext.Request.Path.StartsWithSegments("/") ||
                httpContext.Request.Path.StartsWithSegments("/home", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        internal override async Task ProcessRequestInternal(HttpContext httpContext)
        {
            await httpContext.Response.WriteAsync("Home");
            //Present the following:
            //1. tiled media, random prsentation for now (no categorization).
            //2. navigation for settings and manage media.
        }
    }
}
