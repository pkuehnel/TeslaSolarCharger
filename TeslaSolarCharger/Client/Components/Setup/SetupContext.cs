using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Components.Setup;

/// <summary>
/// What every setup screen needs: the answers so far, what the server makes of them, and the two things a screen
/// ever does besides asking a question - save, and move on. Cascaded from the setup page so a screen does not have
/// to be handed a dozen parameters or reload the state for itself.
/// </summary>
public class SetupContext
{
    public required DtoSetupState State { get; init; }

    /// <summary>
    /// The server's reading of the state: what applies, what is missing, what it would configure on its own. Null
    /// until the first evaluation has come back.
    /// </summary>
    public DtoSetupDecision? Decision { get; set; }

    /// <summary>Writes the current answers to the server, so an interruption never costs the user their work.</summary>
    public required Func<Task> Save { get; init; }

    /// <summary>Re-asks the server what is missing, after something that could have changed the answer.</summary>
    public required Func<Task> RefreshDecision { get; init; }

    /// <summary>Goes to a section, optionally to one device's stage within it.</summary>
    public required Func<string, Guid?, string?, Task> Navigate { get; init; }

    /// <summary>Saves one car draft on its own so it has a database row to hang settings and tests off.</summary>
    public required Func<DtoSetupCarDraft, Task<bool>> SaveCarDraft { get; init; }

    public DtoSetupDeviceStatus? StatusFor(Guid draftId) =>
        Decision?.DeviceStatuses.FirstOrDefault(d => d.DraftId == draftId);

    /// <summary>What still has to be answered for one device, in the words a screen can show.</summary>
    public IReadOnlyList<DtoSetupIssue> BlockersFor(Guid draftId) =>
        StatusFor(draftId)?.ActivationBlockers ?? new List<DtoSetupIssue>();

    public bool IsConfigured(Guid draftId) =>
        StatusFor(draftId)?.ConfigurationStatus == SetupCompletionStatus.Complete;
}
