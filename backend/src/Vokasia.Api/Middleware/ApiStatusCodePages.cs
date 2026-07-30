using Microsoft.AspNetCore.Http;

namespace Vokasia.Api.Middleware;

/// <summary>Produces one predictable RFC 7807-shaped JSON envelope for endpoint misses.</summary>
public static class ApiStatusCodePages
{
    public static bool ShouldWriteJson(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/account/login"))
        {
            // The interactive login form remains HTML. All other API/OAuth requests, including
            // root-level minimal-API routes, use JSON regardless of the client's Accept header.
            return false;
        }

        return true;
    }

    public static object CreatePayload(HttpContext context) => new
    {
        type = "about:blank",
        title = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => "Authentication required",
            StatusCodes.Status403Forbidden => "Access denied",
            StatusCodes.Status404NotFound => "Resource not found",
            _ => "Request failed",
        },
        status = context.Response.StatusCode,
        instance = context.Request.Path.Value,
    };
}
