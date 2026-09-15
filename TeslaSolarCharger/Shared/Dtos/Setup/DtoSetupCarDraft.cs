using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// A car being configured in the setup assistant. Drafts live in the setup state and are only written to the
/// database when the configuration is applied, so editing one can never hand a half configured car to the
/// charging scheduler.
/// </summary>
public class DtoSetupCarDraft
{
    /// <summary>
    /// Stable id of the draft itself. Assignments (e.g. to a charging connector) reference this, so a draft can be
    /// referenced before a database row exists.
    /// </summary>
    public Guid DraftId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Id of the already existing car this draft edits, null while the car exists only as a draft. Imported Tesla
    /// and SmartCar cars already have a row (created unmanaged), so their drafts carry the id from the start.
    /// </summary>
    public int? CarId { get; set; }

    /// <summary>
    /// Which of this car's screens the user is on. Part of the screen's address, so an interruption returns to the
    /// task rather than to the beginning.
    /// </summary>
    public SetupCarStage Stage { get; set; } = SetupCarStage.Identify;

    /// <summary>
    /// What the car is, in the words the owner would use. Collected because it routes the setup and because it is
    /// the question a beginner can actually answer, unlike "is this a Tesla, a SmartCar or a manual car".
    /// </summary>
    public string? Make { get; set; }

    public string? Model { get; set; }

    /// <summary>
    /// Whether a Bluetooth device can sit within a few metres of where this car parks. Asked per car, because one
    /// household can have a car in the garage and another on the street.
    /// </summary>
    public bool? CanPlaceBluetoothDeviceNearCar { get; set; }

    /// <summary>
    /// True when the user chose to type the battery level in themselves rather than connect a data service. Kept
    /// apart from "no connection configured yet": it is a decision, not a gap.
    /// </summary>
    public bool EntersBatteryLevelManually { get; set; }

    public SetupCarConnectionRoute ConnectionRoute { get; set; } = SetupCarConnectionRoute.Undecided;

    /// <summary>
    /// The configuration as it should be saved. Never activated by saving: <see cref="CarBasicConfiguration.ShouldBeManaged"/>
    /// is forced to false until the explicit activation step runs.
    /// </summary>
    public CarBasicConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Whether the user asked for this car to charge automatically once setup finishes. Kept separate from
    /// <see cref="CarBasicConfiguration.ShouldBeManaged"/> so the wish survives while the car is still a draft.
    /// </summary>
    public bool ShouldBeActivated { get; set; } = true;

    /// <summary>
    /// Provenance per configuration property name (e.g. "UsableEnergy"). A property missing here has no known
    /// value; a property marked <see cref="SetupValueSource.UserEntered"/> is never overwritten by a proposal.
    /// </summary>
    public Dictionary<string, SetupValueSource> ValueSources { get; set; } = new();

    /// <summary>
    /// Ids of charging connectors this car should be allowed on. Applied when the draft is materialized, so the
    /// user can assign a car that is not managed yet.
    /// </summary>
    public List<int> AssignedChargingConnectorIds { get; set; } = new();

    public SetupCheckResultState ConnectionCheckState { get; set; } = SetupCheckResultState.NotRun;
    public string? ConnectionCheckMessage { get; set; }
    public DateTimeOffset? ConnectionCheckedAt { get; set; }
}
