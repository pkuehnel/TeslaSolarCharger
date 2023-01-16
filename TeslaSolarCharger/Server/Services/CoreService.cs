using System.Diagnostics;
using System.Reflection;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.TimeProviding;

namespace TeslaSolarCharger.Server.Services;

public class CoreService : ICoreService
{
    private readonly ILogger<CoreService> _logger;
    private readonly IChargingService _chargingService;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CoreService(ILogger<CoreService> logger, IChargingService chargingService, IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _chargingService = chargingService;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<string?> GetCurrentVersion()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentVersion));
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return Task.FromResult(fileVersionInfo.ProductVersion);
    }

    public DtoValue<int> NumberOfRelevantCars()
    {
        _logger.LogTrace("{method}()", nameof(NumberOfRelevantCars));
        return new DtoValue<int>(_chargingService.GetRelevantCarIds().Count);
    }

    public DtoValue<int> HomeBatteryTargetChargingPower()
    {
        _logger.LogTrace("{method}()", nameof(HomeBatteryTargetChargingPower));
        return new DtoValue<int>(_chargingService.GetBatteryTargetChargingPower());
    }

    public DtoValue<bool> IsSolarEdgeInstallation()
    {
        var powerToGridUrl = _configurationWrapper.CurrentPowerToGridUrl();
        return new DtoValue<bool>(!string.IsNullOrEmpty(powerToGridUrl) && powerToGridUrl.StartsWith("http://solaredgeplugin"));
    }

    public DtoValue<DateTime> GetCurrentServerTime()
    {

        return new DtoValue<DateTime>(_dateTimeProvider.Now());
    }

    public void LogVersion()
    {
        _logger.LogTrace("{method}()", nameof(LogVersion));
        _logger.LogInformation("Current version is {productVersion}", GetCurrentVersion().Result);
    }
}
