using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Resources;

internal class NotFound : Resource
{
    internal override bool IsDefault => true;

    internal override bool IsMatch(HttpContext httpContext)
    {
        return false;
    }

    internal override async Task ProcessRequestInternal(HttpContext httpContext)
    {
        await httpContext.Response.WriteAsync("Not Found");
    }
}
