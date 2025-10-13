using System;
using System.Globalization;
using System.Linq;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Helper;

public class NotChargingWithExpectedPowerReasonHelper : INotChargingWithExpectedPowerReasonHelper
{
    private readonly ILogger<NotChargingWithExpectedPowerReasonHelper> _logger;
    private readonly ISettings _settings;
    private readonly IAppStateNotifier _appStateNotifier;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITextLocalizationService _textLocalizationService;

    public NotChargingWithExpectedPowerReasonHelper(ILogger<NotChargingWithExpectedPowerReasonHelper> logger,
        ISettings settings,
        IAppStateNotifier appStateNotifier,
        IDateTimeProvider dateTimeProvider,
        ITextLocalizationService textLocalizationService)
    {
        _logger = logger;
        _settings = settings;
        _appStateNotifier = appStateNotifier;
        _dateTimeProvider = dateTimeProvider;
        _textLocalizationService = textLocalizationService;
    }
    private readonly List<NotChargingWithExpectedPowerReasonTemplate> _genericReasons = new();
    private readonly Dictionary<(int? carId, int? connectorId), List<NotChargingWithExpectedPowerReasonTemplate>> _loadPointSpecificReasons = new();

    public void AddGenericReason(NotChargingWithExpectedPowerReasonTemplate reason)
    {
        if (reason == null)
        {
            throw new ArgumentNullException(nameof(reason));
        }

        _logger.LogTrace("{method}({reason})", nameof(AddGenericReason), reason.LocalizationKey);
        _genericReasons.Add(reason.Clone());
    }

    public void AddLoadPointSpecificReason(int? carId, int? connectorId, NotChargingWithExpectedPowerReasonTemplate reason)
    {
        if (reason == null)
        {
            throw new ArgumentNullException(nameof(reason));
        }

        _logger.LogTrace("{method}({carId}, {connectorId}, {reason})", nameof(AddLoadPointSpecificReason), carId, connectorId, reason.LocalizationKey);
        var key = (carId, connectorId);
        if (!_loadPointSpecificReasons.ContainsKey(key))
        {
            _loadPointSpecificReasons[key] = new List<NotChargingWithExpectedPowerReasonTemplate>();
        }
        _loadPointSpecificReasons[key].Add(reason.Clone());
    }

    public async Task UpdateReasonsInSettings()
    {
        _logger.LogTrace("{method}()", nameof(UpdateReasonsInSettings));
        _settings.GenericNotChargingWithExpectedPowerReasons = new(_genericReasons.Select(reason => reason.Clone()));
        _settings.LoadPointSpecificNotChargingWithExpectedPowerReasons = new(
            _loadPointSpecificReasons.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(reason => reason.Clone()).ToList()));
        var changes = new StateUpdateDto()
        {
            DataType = DataTypeConstants.NotChargingAsExpectedChangeTrigger,
            Timestamp = _dateTimeProvider.DateTimeOffSetUtcNow(),
        };
        await _appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
    }

    public List<DtoNotChargingWithExpectedPowerReason> GetReasons(int? searchCarId, int? searchConnectorId)
    {
        var culture = CultureInfo.CurrentUICulture;
        var allReasons = _settings.GenericNotChargingWithExpectedPowerReasons
            .Select(reason => reason.ToDto(_textLocalizationService, culture))
            .ToList();

        foreach (var loadPointEntry in _settings.LoadPointSpecificNotChargingWithExpectedPowerReasons)
        {
            var entryCarId = loadPointEntry.Key.carId;
            var entryConnectorId = loadPointEntry.Key.connectorId;

            var matchesCarId = (searchCarId != default) && (entryCarId == searchCarId);

            var matchesConnectorId = (searchConnectorId != default) && (entryConnectorId == searchConnectorId);

            if (!matchesCarId && !matchesConnectorId)
            {
                continue;
            }

            allReasons.AddRange(loadPointEntry.Value
                .Select(reason => reason.ToDto(_textLocalizationService, culture)));
        }

        return allReasons;
    }
}
