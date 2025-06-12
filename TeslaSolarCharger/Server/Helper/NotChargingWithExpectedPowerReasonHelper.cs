using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Helper;

public class NotChargingWithExpectedPowerReasonHelper : INotChargingWithExpectedPowerReasonHelper
{
    private readonly ILogger<NotChargingWithExpectedPowerReasonHelper> _logger;
    private readonly ISettings _settings;

    public NotChargingWithExpectedPowerReasonHelper(ILogger<NotChargingWithExpectedPowerReasonHelper> logger,
        ISettings settings)
    {
        _logger = logger;
        _settings = settings;
    }
    private readonly List<DtoNotChargingWithExpectedPowerReason> _genericReasons = new();
    private readonly Dictionary<(int? carId, int? connectorId), List<DtoNotChargingWithExpectedPowerReason>> _loadPointSpecificReasons = new();

    public void AddGenericReason(DtoNotChargingWithExpectedPowerReason reason)
    {
        _logger.LogTrace("{method}({reason})", nameof(AddGenericReason), reason.Reason);
        _genericReasons.Add(reason);
    }

    public void AddLoadPointSpecificReason(int? carId, int? connectorId, DtoNotChargingWithExpectedPowerReason reason)
    {
        _logger.LogTrace("{method}({carId}, {connectorId}, {reason})", nameof(AddLoadPointSpecificReason), carId, connectorId, reason.Reason);
        var key = (carId, connectorId);
        if (!_loadPointSpecificReasons.ContainsKey(key))
        {
            _loadPointSpecificReasons[key] = new List<DtoNotChargingWithExpectedPowerReason>();
        }
        _loadPointSpecificReasons[key].Add(reason);
    }

    public void UpdateReasonsInSettings()
    {
        _logger.LogTrace("{method}()", nameof(UpdateReasonsInSettings));
        _settings.GenericNotChargingWithExpectedPowerReasons = new(_genericReasons);
        _settings.LoadPointSpecificNotChargingWithExpectedPowerReasons = new(_loadPointSpecificReasons);
    }

    public List<DtoNotChargingWithExpectedPowerReason> GetReasons(int? searchCarId, int? searchConnectorId)
    {
        var allReasons = new List<DtoNotChargingWithExpectedPowerReason>(_settings.GenericNotChargingWithExpectedPowerReasons);

        foreach (var loadPointEntry in _settings.LoadPointSpecificNotChargingWithExpectedPowerReasons)
        {
            var entryCarId = loadPointEntry.Key.carId;
            var entryConnectorId = loadPointEntry.Key.connectorId;

            var matchesCarId = (searchCarId != default) && (entryCarId == searchCarId);

            var matchesConnectorId = (searchConnectorId != default) && (entryConnectorId == searchConnectorId);

            if (matchesCarId || matchesConnectorId)
            {
                allReasons.AddRange(loadPointEntry.Value);
            }
        }

        return allReasons;
    }
}
