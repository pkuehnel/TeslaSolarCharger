using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Everything the setup assistant knows about an installation that is being configured. Replaces the positional
/// <see cref="DtoSetupCache"/>: steps are identified by a stable key, equipment is described by per device drafts,
/// and the answers survive a reload, an external authorization and a partially failed save.
/// </summary>
public class DtoSetupState
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Schema version of this state. Raised whenever the shape changes so an older persisted state can be migrated
    /// instead of silently losing answers.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public SetupStepKey CurrentStep { get; set; } = SetupStepKey.Welcome;
    public List<SetupStepKey> CompletedSteps { get; set; } = new();

    /// <summary>
    /// Whether the installation has solar panels. Null means unanswered, which is deliberately different from "no":
    /// an unanswered question must not silently configure the installation as having no solar system.
    /// </summary>
    public bool? HasPvSystem { get; set; }

    /// <summary>
    /// Whether the installation has a home battery. Asked independently of <see cref="HasPvSystem"/> so a battery
    /// without solar panels does not disappear from setup.
    /// </summary>
    public bool? HasHomeBattery { get; set; }

    public DtoBaseConfiguration Configuration { get; set; } = new();
    public DtoChargePrice? ChargePrice { get; set; }
    public List<FixedPrice> FixedPrices { get; set; } = new();

    /// <summary>Intake answers of the legacy questionnaire. Kept so a migrated state does not lose them.</summary>
    public DtoCarsChargingSetupAnswers CarsChargingSetup { get; set; } = new();

    public List<DtoSetupCarDraft> CarDrafts { get; set; } = new();
    public List<DtoSetupChargerDraft> ChargerDrafts { get; set; } = new();

    /// <summary>
    /// Provenance per base configuration property name. A property recorded as UserEntered is never replaced by a
    /// proposed value, which is what keeps an existing installation's explicit choices.
    /// </summary>
    public Dictionary<string, SetupValueSource> ValueSources { get; set; } = new();

    /// <summary>The task the user left setup for, e.g. an OAuth redirect. Null while nothing is pending.</summary>
    public DtoSetupPendingOperation? PendingOperation { get; set; }

    /// <summary>Application steps that already succeeded, so a retry does not repeat them.</summary>
    public List<DtoSetupOperationRecord> CompletedOperations { get; set; } = new();

    /// <summary>
    /// When the server last wrote this state. Used to detect that Base Configuration changed the real
    /// configuration behind the assistant's back, so a stale draft does not overwrite it.
    /// </summary>
    public DateTimeOffset? LastSavedAt { get; set; }

    public bool IsStepCompleted(SetupStepKey step) => CompletedSteps.Contains(step);

    public DtoSetupCarDraft? FindCarDraft(Guid draftId) => CarDrafts.FirstOrDefault(d => d.DraftId == draftId);
}
