using Newtonsoft.Json;
using System.Linq.Expressions;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.TeslaPowerwall;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.TeslaPowerwall;

public class TeslaPowerwallHomeBatteryModeService : IHomeBatteryModeSetupService
{
    private readonly ILogger<TeslaPowerwallHomeBatteryModeService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public TeslaPowerwallHomeBatteryModeService(ILogger<TeslaPowerwallHomeBatteryModeService> logger,
        IServiceScopeFactory serviceScopeFactory, ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var templateValueGatherType = TemplateValueGatherType.TeslaPowerwallFleetApi;
        Expression<Func<TemplateValueConfiguration, bool>> expression = c => c.GatherType == templateValueGatherType;
        var configs = await _templateValueConfigurationService
            .GetConfigurationsByPredicateAsync(expression).ConfigureAwait(false);
        var result = new List<DtoHomeBatteryModeController>();
        foreach (var config in configs)
        {
            if (config.Configuration == default)
            {
                continue;
            }
            var typedConfig = config.Configuration.ToObject<DtoTeslaPowerwallTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!typedConfig.EnableHomeBatteryControl || typedConfig.EnergySiteId == default)
            {
                continue;
            }
            var energySiteId = typedConfig.EnergySiteId.Value;
            var normalModeReservePercent = typedConfig.NormalModeBackupReservePercent;
            var serviceScopeFactory = _serviceScopeFactory;
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    using var executionScope = serviceScopeFactory.CreateScope();
                    var settings = executionScope.ServiceProvider.GetRequiredService<ISettings>();
                    var configurationWrapper = executionScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
                    var backupReservePercent = GetBackupReservePercent(mode, normalModeReservePercent,
                        settings.HomeBatterySoc, configurationWrapper.HomeBatteryMaxChargeSoc());
                    var teslaFleetApiService = executionScope.ServiceProvider.GetRequiredService<ITeslaFleetApiService>();
                    var teslaResponse = await teslaFleetApiService
                        .SetEnergySiteBackupReserve(energySiteId.ToString(), backupReservePercent)
                        .ConfigureAwait(false);
                    if (!teslaResponse.StatusCode.IsSuccessStatusCode())
                    {
                        throw new InvalidOperationException(
                            $"Setting backup reserve did not result in success status code: {teslaResponse.StatusCode}");
                    }
                },
                //The backup reserve persists on the Powerwall, so no periodic rewrite is required.
                default));
        }
        return result;
    }

    /// <summary>
    /// The Powerwall is controlled via its backup reserve: In hold mode the reserve is set to the current soc so the
    /// battery does not discharge below its current level, in charge mode it is set to the max charge soc so the
    /// battery charges up to it.
    /// </summary>
    public static int GetBackupReservePercent(HomeBatteryMode mode, int normalModeReservePercent, int? currentSoc, int maxChargeSoc)
    {
        return mode switch
        {
            HomeBatteryMode.Normal => normalModeReservePercent,
            HomeBatteryMode.Hold => Math.Min(100, Math.Max(
                currentSoc ?? throw new InvalidOperationException("Home battery soc is required to set hold mode on a Powerwall"),
                normalModeReservePercent)),
            HomeBatteryMode.Charge => maxChargeSoc,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written to Powerwall"),
        };
    }
}
