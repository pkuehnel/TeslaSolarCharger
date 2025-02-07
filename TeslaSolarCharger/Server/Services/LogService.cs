using Serilog.Sinks.InMemory;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class LogService(ILogger<LogService> logger) : ILogService
{
    public string GetLogs()
    {
        logger.LogTrace("{method}", nameof(GetLogs));
        var events = InMemorySink.Instance.LogEvents;
        var logs = events.Select(e => e.RenderMessage()).ToList();
        return string.Join(Environment.NewLine, logs);
    }
}
