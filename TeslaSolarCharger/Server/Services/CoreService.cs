using System.Diagnostics;
using System.Reflection;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class CoreService : ICoreService
{
    private readonly ILogger<CoreService> _logger;

    public CoreService(ILogger<CoreService> logger)
    {
        _logger = logger;
    }

    public Task<string?> GetCurrentVersion()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentVersion));
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return Task.FromResult(fileVersionInfo.ProductVersion);
    }

    public void LogVersion()
    {
        _logger.LogTrace("{method}()", nameof(LogVersion));
        _logger.LogInformation("Current version is {productVersion}", GetCurrentVersion().Result);
    }
}
