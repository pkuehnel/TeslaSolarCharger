using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class LoadPointManagementService : ILoadPointManagementService
{
    private readonly ILogger<LoadPointManagementService> _logger;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IIssueKeys _issueKeys;

    public LoadPointManagementService(ILogger<LoadPointManagementService> logger,
        ISettings settings,
        ITeslaSolarChargerContext context,
        IConfigurationWrapper configurationWrapper,
        IErrorHandlingService errorHandlingService,
        IIssueKeys issueKeys)
    {
        _logger = logger;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
    }

    public async Task<List<DtoLoadpoint>> GetPluggedInLoadPoints()
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var pluggedInCars = _settings.Cars
            .Where(c => c.IsHomeGeofence == true && c.PluggedIn == true)
            .ToList();
        var pluggedInChargingConnectors = _settings.OcppConnectorStates
            .Where(c => c.Value.IsPluggedIn.Value)
            .ToDictionary();
        var result = new List<DtoLoadpoint>();
        foreach (var pluggedInCar in pluggedInCars)
        {
            result.Add(new()
            {
                Car = pluggedInCar,
            });
        }
        await AddPluggedInChargingConnectorsAndTryAutomatchToExistingLoadpoints(pluggedInChargingConnectors, result).ConfigureAwait(false);
        return result;
    }

    private async Task AddPluggedInChargingConnectorsAndTryAutomatchToExistingLoadpoints(Dictionary<int, DtoOcppConnectorState> pluggedInChargingConnectors, List<DtoLoadpoint> result)
    {
        var anyConnectorWithMultipleMatchingCars = false;
        foreach (var pluggedInCharingConnector in pluggedInChargingConnectors)
        {
            if (pluggedInCharingConnector.Value.LastPluggedIn != default)
            {
                var matchingLoadPoints = result
                    .Where(l => l.Car?.LastPluggedIn < pluggedInCharingConnector.Value.LastPluggedIn.Value.Add(_configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector())
                                && l.Car?.LastPluggedIn > pluggedInCharingConnector.Value.LastPluggedIn.Value.Add(-_configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector()))
                    .ToList();
                if (matchingLoadPoints.Count < 1)
                {
                    _logger.LogTrace("Did not find any car that was plugged in at the same time as chargepoint connector {chargepointConnectorID}", pluggedInCharingConnector.Key);
                    result.Add(new()
                    {
                        OcppConnectorId = pluggedInCharingConnector.Key,
                        OcppConnectorState = pluggedInCharingConnector.Value,
                    });
                }
                else if (matchingLoadPoints.Count == 1)
                {
                    var matchingLoadPoint = matchingLoadPoints.First();
                    _logger.LogTrace("Car {carId} was plugged in at the same time as ChargingConnector {chargingConnectorId}. Combining to one loadpoint.", matchingLoadPoint.Car?.Id, pluggedInCharingConnector.Key);
                    matchingLoadPoint.OcppConnectorId = pluggedInCharingConnector.Key;
                    matchingLoadPoint.OcppConnectorState = pluggedInCharingConnector.Value;
                }
                else
                {
                    _logger.LogError("More than one car ({carIdList}) was plugged in at the same time as Charging connector {chargingConnectorId}",
                        string.Join(", ", matchingLoadPoints.Select(l => l.Car?.Id).ToList()), pluggedInCharingConnector.Key);
                    anyConnectorWithMultipleMatchingCars = true;
                    result.Add(new()
                    {
                        OcppConnectorId = pluggedInCharingConnector.Key,
                        OcppConnectorState = pluggedInCharingConnector.Value,
                    });
                }
                            
            }
        }

        if (anyConnectorWithMultipleMatchingCars)
        {
            await _errorHandlingService.HandleError(nameof(LoadPointManagementService), nameof(AddPluggedInChargingConnectorsAndTryAutomatchToExistingLoadpoints),
                "Could not autodetect cars plugged in in charging points",
                $"At least two cars were connected nearly at the same time as one charging connector. To autodetect which car is charging on which charging connector plug out all cars but one and then plug in the cars but between each plugin wait at least {_configurationWrapper.MaxPluggedInTimeDifferenceToMatchCarAndOcppConnector().TotalSeconds * 3} seconds.",
                _issueKeys.MultipleCarsMatchChargingConnector, null, null).ConfigureAwait(false);
        }
        else
        {
            await _errorHandlingService.HandleErrorResolved(_issueKeys.MultipleCarsMatchChargingConnector, null).ConfigureAwait(false);
        }
    }
}
