using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TeslaSolarCharger.Server.Middlewares;

/// <summary>
/// Adds security headers to HTTP responses to improve security.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent browsers from sniffing the content type
        if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        }

        // Control how much referrer information is sent with requests
        if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
        {
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        }

        // Note: X-Frame-Options is intentionally omitted to allow embedding in Home Assistant dashboards (e.g. via iframe).
        // If needed in the future, consider using Content-Security-Policy frame-ancestors directive instead.

        await next(context);
    }
}
