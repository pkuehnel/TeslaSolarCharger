namespace TeslaSolarCharger.Server.Middlewares;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        // Intentionally omitting X-Frame-Options to allow iframe embedding (e.g. Home Assistant)

        await next(context);
    }
}
