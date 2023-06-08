using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class BaseConfigurationService : IBaseConfigurationService
{
    private readonly ILogger<BaseConfigurationService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly JobManager _jobManager;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly ISolarMqttService _solarMqttService;
    private readonly ISettings _settings;
    private readonly IPvValueService _pvValueService;

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger, IConfigurationWrapper configurationWrapper,
        JobManager jobManager, ITeslaMateMqttService teslaMateMqttService, ISolarMqttService solarMqttService,
        ISettings settings, IPvValueService pvValueService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _jobManager = jobManager;
        _teslaMateMqttService = teslaMateMqttService;
        _solarMqttService = solarMqttService;
        _settings = settings;
        _pvValueService = pvValueService;
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        _logger.LogTrace("{method}({@baseConfiguration})", nameof(UpdateBaseConfigurationAsync), baseConfiguration);
        await _jobManager.StopJobs().ConfigureAwait(false);
        await _configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
        await _teslaMateMqttService.ConnectMqttClient().ConfigureAwait(false);
        await _solarMqttService.ConnectMqttClient().ConfigureAwait(false);
        if (_configurationWrapper.FrontendConfiguration()?.GridValueSource == SolarValueSource.None)
        {
            _settings.Overage = null;
            _pvValueService.ClearOverageValues();
        }

        if (_configurationWrapper.FrontendConfiguration()?.HomeBatteryValuesSource == SolarValueSource.None)
        {
            _settings.HomeBatteryPower = null;
            _settings.HomeBatterySoc = null;
        }

        if (_configurationWrapper.FrontendConfiguration()?.InverterValueSource == SolarValueSource.None)
        {
            _settings.InverterPower = null;
        }

        await _jobManager.StartJobs().ConfigureAwait(false);
    }

    public async Task UpdateMaxCombinedCurrent(int? maxCombinedCurrent)
    {
        var baseConfiguration = await _configurationWrapper.GetBaseConfigurationAsync().ConfigureAwait(false);
        baseConfiguration.MaxCombinedCurrent = maxCombinedCurrent;
        await _configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
    }

    public void UpdatePowerBuffer(int powerBuffer)
    {
        _settings.PowerBuffer = powerBuffer;
    }
}
