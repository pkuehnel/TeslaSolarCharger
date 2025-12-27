using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TeslaSolarCharger.Server.Middlewares
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

            await _next(context);
        }
    }
}
