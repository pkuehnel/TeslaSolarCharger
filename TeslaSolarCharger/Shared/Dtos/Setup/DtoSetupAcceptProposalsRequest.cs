namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Accepting proposals needs both the state being edited and the subset of proposals the user agreed to, so the
/// two travel together in one request.
/// </summary>
public class DtoSetupAcceptProposalsRequest
{
    public DtoSetupState SetupState { get; set; } = new();
    public List<DtoSetupProposedValue> Proposals { get; set; } = new();
}
