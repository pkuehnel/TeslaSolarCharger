using PkSoftwareService.Custom.Backend;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Middlewares
{
    public class StartupCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<StartupCheckMiddleware> _logger;
        private readonly IInMemorySink _inMemorySink;

        public StartupCheckMiddleware(RequestDelegate next, ILogger<StartupCheckMiddleware> logger, IInMemorySink inMemorySink)
        {
            _next = next;
            _logger = logger;
            _inMemorySink = inMemorySink;
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

            // Allow API calls that check startup status and get logs
            if (path.Contains("/api/hello/isstartupcompleted") ||
                path.Contains("/api/debug") ||
                path.EndsWith(".css"))
            {
                await _next(context);
                return;
            }

            _logger.LogDebug("Blocking request to {Path} - startup not complete", path);

            // Return a simple HTML page with startup message and logs
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";

            var html = GetStartupHtml();
            await context.Response.WriteAsync(html);
        }

        private string GetStartupHtml()
        {
            var title = "Application Starting";

            // Get current logs (last 30 lines)
            var logLinesCount = 30;
            var logs = _inMemorySink.GetLogs(logLinesCount);
            var logsHtml = string.Empty;

            if (logs.Any())
            {
                // Format logs for HTML display
                var logLines = logs.Select(log => System.Web.HttpUtility.HtmlEncode(log))
                                   .Select(log => $"<div class=\"log-line\">{log}</div>");
                logsHtml = string.Join("\n", logLines);
            }
            else
            {
                logsHtml = "<div class=\"log-line\">No logs available yet...</div>";
            }

            var refreshScript = $@"
                <script>
                    // Function to fetch and update logs
                    async function updateLogs() {{
                        try {{
                            const response = await fetch('/api/Debug/GetLogs?tail={logLinesCount}');
                            if (response.ok) {{
                                const logs = await response.json();
                                const container = document.getElementById('logs-container');
                                if (container && logs) {{
                                    // Always update logs when we get a response
                                    if (logs.length > 0) {{
                                        container.innerHTML = logs.map(log => 
                                            `<div class='log-line'>${{escapeHtml(log)}}</div>`
                                        ).join('\n');
                                    }} else {{
                                        container.innerHTML = '<div class=""log-line"">No logs available yet...</div>';
                                    }}
                                }}
                            }}
                        }} catch (error) {{
                            console.error('Error fetching logs:', error);
                        }}
                    }}
                    
                    // Helper function to escape HTML
                    function escapeHtml(text) {{
                        const div = document.createElement('div');
                        div.textContent = text;
                        return div.innerHTML;
                    }}
                    
                    // Check startup status every 2 seconds
                    setInterval(async function() {{
                        try {{
                            const response = await fetch('/api/Hello/IsStartupCompleted');
                            if (response.ok) {{
                                const data = await response.json();
                                if (data && data.value === true) {{
                                    // Reload the page when startup is complete
                                    window.location.reload();
                                }}
                            }}
                        }} catch (error) {{
                            console.error('Error checking startup status:', error);
                        }}
                    }}, 2000);
                    
                    // Update logs every second
                    setInterval(updateLogs, 10000);
                    
                    // Initial log update
                    updateLogs();
                </script>";

            var styles = @"
                <style>
                    .logs-section {
                        margin-top: 20px;
                        border: 1px solid #dee2e6;
                        border-radius: 0.25rem;
                        background-color: #f8f9fa;
                    }
                    .logs-header {
                        padding: 10px 15px;
                        background-color: #e9ecef;
                        border-bottom: 1px solid #dee2e6;
                        font-weight: bold;
                    }
                    .logs-container {
                        padding: 10px;
                        font-family: 'Courier New', Courier, monospace;
                        font-size: 12px;
                        background-color: #1e1e1e;
                        color: #d4d4d4;
                    }
                    .log-line {
                        white-space: pre-wrap;
                        word-wrap: break-word;
                        margin-bottom: 2px;
                        padding: 2px 0;
                    }
                    .log-line:hover {
                        background-color: #2d2d2d;
                    }
                    .spinner {
                        display: inline-block;
                        width: 20px;
                        height: 20px;
                        border: 3px solid rgba(0,0,0,.1);
                        border-radius: 50%;
                        border-top-color: #007bff;
                        animation: spin 1s ease-in-out infinite;
                    }
                    @keyframes spin {
                        to { transform: rotate(360deg); }
                    }
                </style>";

            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>{title}</title>
    <link href=""css/bootstrap/bootstrap.min.css"" rel=""stylesheet"" />
    {styles}
</head>
<body>
    <div class=""m-2"">
        <div class=""alert alert-warning"" role=""alert"">
            <div class=""d-flex align-items-center mb-2"">
                <div class=""spinner me-3""></div>
                <strong>Application starting up - Please read the complete message.</strong>
            </div>
            <hr />
            <div>TeslaSolarCharger is starting up. Depending on your device and the size of your database, this can take up to 30 minutes after an update. Do NOT stop or restart the container, as this might damage the database. After the startup is finished, the page will reload automatically.</div>
        </div>
        
        <div class=""logs-section"">
            <div class=""logs-header"">
                <span>Startup Logs (Last {logLinesCount} lines)</span>
            </div>
            <div id=""logs-container"" class=""logs-container"">
                {logsHtml}
            </div>
        </div>
    </div>
    {refreshScript}
</body>
</html>";
        }
    }
}
