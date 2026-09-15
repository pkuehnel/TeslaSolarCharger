using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// A charging station connector being configured in the setup assistant. A charging station connects itself over
/// OCPP, so unlike a car draft this always references an existing connector; the draft only holds the answers and
/// the activation wish until the configuration is applied.
/// </summary>
public class DtoSetupChargerDraft
{
    public Guid DraftId { get; set; } = Guid.NewGuid();

    /// <summary>Database id of the connector. Null while the charging station has not connected yet.</summary>
    public int? ConnectorId { get; set; }

    public int? ChargingStationId { get; set; }

    /// <summary>Chargepoint id the user was told to enter in the charger's OCPP settings.</summary>
    public string? ChargepointId { get; set; }

    /// <summary>Which of this charger's screens the user is on.</summary>
    public SetupChargerStage Stage { get; set; } = SetupChargerStage.Connect;

    /// <summary>What the user calls this charger. Only for telling several chargers apart on screen.</summary>
    public string? DisplayName { get; set; }

    public bool ShouldBeActivated { get; set; } = true;

    /// <summary>Draft ids of the cars allowed on this connector, including cars without a database row yet.</summary>
    public List<Guid> AllowedCarDraftIds { get; set; } = new();

    public bool AllowGuestCars { get; set; }

    public SetupCheckResultState ConnectionCheckState { get; set; } = SetupCheckResultState.NotRun;
    public string? ConnectionCheckMessage { get; set; }
    public DateTimeOffset? ConnectionCheckedAt { get; set; }
}
