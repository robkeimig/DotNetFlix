using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Resources;

//A more tabular presentation of media content.
internal class Media : Resource
{
    internal override bool IsMatch(HttpContext httpContext)
    {
        return false;
    }

    internal override Task ProcessRequestInternal(HttpContext httpContext)
    {
        throw new NotImplementedException();
    }
}
