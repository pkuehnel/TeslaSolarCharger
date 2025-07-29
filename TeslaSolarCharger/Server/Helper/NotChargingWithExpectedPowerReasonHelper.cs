using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Helper;

public class NotChargingWithExpectedPowerReasonHelper : INotChargingWithExpectedPowerReasonHelper
{
    private readonly ILogger<NotChargingWithExpectedPowerReasonHelper> _logger;
    private readonly ISettings _settings;
    private readonly IAppStateNotifier _appStateNotifier;
    private readonly IDateTimeProvider _dateTimeProvider;

    public NotChargingWithExpectedPowerReasonHelper(ILogger<NotChargingWithExpectedPowerReasonHelper> logger,
        ISettings settings,
        IAppStateNotifier appStateNotifier,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _appStateNotifier = appStateNotifier;
        _dateTimeProvider = dateTimeProvider;
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

    public async Task UpdateReasonsInSettings()
    {
        _logger.LogTrace("{method}()", nameof(UpdateReasonsInSettings));
        _settings.GenericNotChargingWithExpectedPowerReasons = new(_genericReasons);
        _settings.LoadPointSpecificNotChargingWithExpectedPowerReasons = new(_loadPointSpecificReasons);
        var changes = new StateUpdateDto()
        {
            DataType = DataTypeConstants.NotChargingAsExpectedChangeTrigger,
            Timestamp = _dateTimeProvider.DateTimeOffSetUtcNow(),
        };
        await _appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
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
