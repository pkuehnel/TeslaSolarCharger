using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Services;

public class BaseConfigurationService : IBaseConfigurationService
{
    private readonly ILogger<BaseConfigurationService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly JobManager _jobManager;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly ISolarMqttService _solarMqttService;

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger, IConfigurationWrapper configurationWrapper,
        JobManager jobManager, ITeslaMateMqttService teslaMateMqttService, ISolarMqttService solarMqttService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _jobManager = jobManager;
        _teslaMateMqttService = teslaMateMqttService;
        _solarMqttService = solarMqttService;
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        _logger.LogTrace("{method}({@baseConfiguration})", nameof(UpdateBaseConfigurationAsync), baseConfiguration);
        await _jobManager.StopJobs().ConfigureAwait(false);
        await _configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
        await _teslaMateMqttService.ConnectMqttClient().ConfigureAwait(false);
        await _solarMqttService.ConnectMqttClient().ConfigureAwait(false);
        await _jobManager.StartJobs().ConfigureAwait(false);
    }
}
