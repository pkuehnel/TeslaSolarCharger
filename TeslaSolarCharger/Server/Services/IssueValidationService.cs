using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class IssueValidationService : IIssueValidationService
{
    private readonly ILogger<IssueValidationService> _logger;
    private readonly ITeslaService _teslaService;
    private readonly IPvValueService _pvValueService;
    private readonly ISettings _settings;
    private readonly IMqttService _mqttService;
    private readonly IPossibleIssues _possibleIssues;
    private readonly IssueKeys _issueKeys;
    private readonly GlobalConstants _globalConstants;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITeslamateContext _teslamateContext;

    public IssueValidationService(ILogger<IssueValidationService> logger,
        ITeslaService teslaService, IPvValueService pvValueService, ISettings settings,
        IMqttService mqttService, IPossibleIssues possibleIssues, IssueKeys issueKeys,
        GlobalConstants globalConstants, IConfigurationWrapper configurationWrapper,
        ITeslamateContext teslamateContext)
    {
        _logger = logger;
        _teslaService = teslaService;
        _pvValueService = pvValueService;
        _settings = settings;
        _mqttService = mqttService;
        _possibleIssues = possibleIssues;
        _issueKeys = issueKeys;
        _globalConstants = globalConstants;
        _configurationWrapper = configurationWrapper;
        _teslamateContext = teslamateContext;
    }

    public async Task<List<Issue>> RefreshIssues()
    {
        _logger.LogTrace("{method}()", nameof(RefreshIssues));
        var issueList = new List<Issue>();
        issueList.AddRange(GetMqttIssues());
        issueList.AddRange(PvValueIssues());
        issueList.AddRange(await GetTeslaMateApiIssues().ConfigureAwait(false));
        issueList.AddRange(await GetDatabaseIssues().ConfigureAwait(false));
        return issueList;
    }

    private async Task<List<Issue>> GetDatabaseIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetDatabaseIssues));
        var issues = new List<Issue>();
        List<short> carIds;
        try
        {
            carIds = await _teslamateContext.Cars.Select(car => car.Id).ToListAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.DatabaseNotAvailable));
            return issues;
        }

        var geofenceNames = _teslamateContext.Geofences.Select(ge => ge.Name).ToList();
        var configuredGeofence = _configurationWrapper.GeoFence();
        if (!geofenceNames.Any(g => g == configuredGeofence))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.GeofenceNotAvailable));
        }

        var configuredCarIds = _configurationWrapper.CarPriorities();

        foreach (var configuredCarId in configuredCarIds)
        {
            if (!carIds.Any(i => i == configuredCarId))
            {
                issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.CarIdNotAvailable));
            }
        }

        return issues;
    }

    private async Task<List<Issue>> GetTeslaMateApiIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetTeslaMateApiIssues));
        var issues = new List<Issue>();
        var teslaMateBaseUrl = _configurationWrapper.TeslaMateApiBaseUrl();
        var getAllCarsUrl = $"{teslaMateBaseUrl}/api/v1/cars";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(1);
        try
        {
            var resultString = await httpClient.GetStringAsync(getAllCarsUrl).ConfigureAwait(false);
            if (string.IsNullOrEmpty(resultString))
            {
                issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.TeslaMateApiNotAvailable));
            }
        }
        catch (Exception)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.TeslaMateApiNotAvailable));
        }
        return issues;
    }

    private List<Issue> GetMqttIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetMqttIssues));
        var issues = new List<Issue>();
        if (!_mqttService.IsMqttClientConnected)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.MqttNotConnected));
        }

        if (_settings.Cars.Any(c => c.CarState.SocLimit == null || c.CarState.SocLimit < _globalConstants.MinSocLimit))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.CarSocLimitNotReadable));
        }

        if (_settings.Cars.Any(c => c.CarState.SoC == null))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.CarSocNotReadable));
        }

        return issues;
    }

    private List<Issue> PvValueIssues()
    {
        _logger.LogTrace("{method}()", nameof(GetMqttIssues));
        var issues = new List<Issue>();
        if (_settings.Overage == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.GridPowerNotAvailable));
        }

        if (!string.IsNullOrWhiteSpace(_configurationWrapper.CurrentInverterPowerUrl()) && _settings.InverterPower == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.InverterPowerNotAvailable));
        }

        if (!string.IsNullOrWhiteSpace(_configurationWrapper.HomeBatterySocUrl()) && _settings.HomeBatterySoc == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatterySocNotAvailable));
        }

        if (!string.IsNullOrWhiteSpace(_configurationWrapper.HomeBatterySocUrl()) && _settings.HomeBatterySoc != null && (_settings.HomeBatterySoc > 100 || _settings.HomeBatterySoc < 0))
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatterySocNotPlausible));
        }

        if (!string.IsNullOrWhiteSpace(_configurationWrapper.HomeBatteryPowerUrl()) && _settings.HomeBatteryPower == null)
        {
            issues.Add(_possibleIssues.GetIssueByKey(_issueKeys.HomeBatteryPowerNotAvailable));
        }

        return issues;
    }
}
