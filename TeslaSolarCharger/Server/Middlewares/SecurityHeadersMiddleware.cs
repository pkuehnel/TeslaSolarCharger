using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TeslaSolarCharger.Server.Middlewares;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers to response
        // X-Content-Type-Options: nosniff - Prevents browsers from sniffing the MIME type
        // Referrer-Policy: strict-origin-when-cross-origin - Controls how much referrer info is sent

        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        await _next(context);
    }
}
