using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Fronius;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Fronius;

/// <summary>
/// Battery control via the Fronius battery management time of use configuration. Only blocking discharge is
/// supported, forced charging is not available via the Solar API. Note: existing time of use settings in the
/// inverter configuration are overwritten.
/// </summary>
public class FroniusSolarApiHomeBatteryModeService : IHomeBatteryModeSetupService
{
    private const string EmptyTimeOfUseBody = """{"timeofuse":[]}""";
    private const string BlockDischargeTimeOfUseBody =
        """{"timeofuse":[{"Active":true,"Power":0,"ScheduleType":"DISCHARGE_MAX","TimeTable":{"Start":"00:00","End":"23:59"},"Weekdays":{"Mon":true,"Tue":true,"Wed":true,"Thu":true,"Fri":true,"Sat":true,"Sun":true}}]}""";

    private readonly ILogger<FroniusSolarApiHomeBatteryModeService> _logger;
    private readonly ITemplateValueConfigurationService _templateValueConfigurationService;

    public FroniusSolarApiHomeBatteryModeService(ILogger<FroniusSolarApiHomeBatteryModeService> logger,
        ITemplateValueConfigurationService templateValueConfigurationService)
    {
        _logger = logger;
        _templateValueConfigurationService = templateValueConfigurationService;
    }

    public async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControllersAsync));
        var templateValueGatherType = TemplateValueGatherType.FroniusSolarApiV1;
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
            var typedConfig = config.Configuration.ToObject<DtoFroniusSolarApiTemplateValueConfiguration>();
            if (typedConfig == default)
            {
                _logger.LogError("Could not deserialize configuration {gatherType} for ID {id}. Json is: {json}",
                    config.GatherType, config.Id, config.Configuration.ToString(Formatting.None));
                continue;
            }
            if (!typedConfig.EnableHomeBatteryControl || string.IsNullOrEmpty(typedConfig.Host) || string.IsNullOrEmpty(typedConfig.Password))
            {
                continue;
            }
            var host = typedConfig.Host;
            var username = typedConfig.Username;
            var password = typedConfig.Password;
            var configPath = typedConfig.UseApiConfigPath ? "/api/config" : "/config";
            result.Add(new DtoHomeBatteryModeController(config.Id, config.Name ?? string.Empty,
                async (mode, ct) =>
                {
                    var body = mode switch
                    {
                        HomeBatteryMode.Normal => EmptyTimeOfUseBody,
                        HomeBatteryMode.Hold => BlockDischargeTimeOfUseBody,
                        HomeBatteryMode.Charge => throw new NotSupportedException(
                            "Forced charging is not supported via the Fronius Solar API"),
                        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written via Fronius Solar API"),
                    };
                    //The Fronius configuration endpoints require digest authentication
                    using var handler = new HttpClientHandler
                    {
                        Credentials = new NetworkCredential(username, password),
                    };
                    using var httpClient = new HttpClient(handler);
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    using var content = new StringContent(body, Encoding.UTF8, "application/json");
                    var response = await httpClient
                        .PostAsync($"http://{host}{configPath}/timeofuse", content, ct)
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            $"Setting Fronius time of use configuration failed with status code {response.StatusCode}: {responseBody}");
                    }
                },
                //The time of use configuration persists on the inverter, so no periodic rewrite is required.
                default));
        }
        return result;
    }
}
