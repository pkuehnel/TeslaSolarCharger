using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Middlewares
{
    public class StartupCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<StartupCheckMiddleware> _logger;

        public StartupCheckMiddleware(RequestDelegate next, ILogger<StartupCheckMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISettings settings)
        {
            // Check if startup is complete
            if (settings.IsStartupCompleted)
            {
                // App is ready, continue with normal request processing
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value?.ToLower() ?? "";

            // Allow API calls that check startup status
            if (path.Contains("/api/hello/isstartupcompleted"))
            {
                await _next(context);
                return;
            }

            // Block WebAssembly files and main app requests
            if (path == "/" ||
                path == "/index.html" ||
                path.EndsWith(".dll") ||
                path.EndsWith(".wasm") ||
                path.Contains("_framework") ||
                path.EndsWith(".blat") ||
                path.EndsWith(".dat") ||
                path.EndsWith(".gz") ||
                path.EndsWith(".br"))
            {
                _logger.LogDebug("Blocking request to {Path} - startup not complete", path);

                // Return a simple HTML page with startup message
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";

                var html = GetStartupHtml();
                await context.Response.WriteAsync(html);
                return;
            }

            // Allow other resources (CSS, images, etc.)
            await _next(context);
        }

        private string GetStartupHtml()
        {
            var title = "Application Starting";
            var refreshScript = @"
                <script>
                    // Check startup status every 2 seconds
                    setInterval(async function() {
                        try {
                            const response = await fetch('/api/Hello/IsStartupCompleted');
                            if (response.ok) {
                                const data = await response.json();
                                if (data && data.value === true) {
                                    // Reload the page when startup is complete
                                    window.location.reload();
                                }
                            }
                        } catch (error) {
                            console.error('Error checking startup status:', error);
                        }
                    }, 2000);
                </script>";

            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>{title}</title>
    <link href=""css/bootstrap/bootstrap.min.css"" rel=""stylesheet"" />
</head>
<body>
    <div class=""m-2"">
        <div class=""alert alert-warning"" role=""alert"">
            <div>Application starting up - Read to the end!!</div>
            <hr />
            <div>TeslaSolarCharger is starting up. Depending on your device and the size of your database, this can take up to 30 minutes after an update. Do NOT stop or restart the container, as this might damage the database. For further information, check the logs. After the startup is finished, the page will reload automatically. Note: The duration this takes is not the same in each update; just because it took just a few seconds the last few months does not mean it is not normal if it takes 30 minutes this time.</div>
        </div>
    </div>
    {refreshScript}
</body>
</html>";
        }
    }
}
