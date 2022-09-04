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

    public IssueValidationService(ILogger<IssueValidationService> logger,
        ITeslaService teslaService, IPvValueService pvValueService, ISettings settings,
        IMqttService mqttService, IPossibleIssues possibleIssues, IssueKeys issueKeys,
        GlobalConstants globalConstants, IConfigurationWrapper configurationWrapper)
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
    }

    public List<Issue> RefreshIssues()
    {
        _logger.LogTrace("{method}()", nameof(RefreshIssues));
        var issueList = new List<Issue>();
        issueList.AddRange(GetMqttIssues());
        issueList.AddRange(PvValueIssues());
        return issueList;
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
