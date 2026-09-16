namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Structured outcome of applying a setup state. Every operation reports for itself, so the finish screen can show
/// exactly which part failed and offer a retry instead of claiming success.
/// </summary>
public class DtoSetupApplicationResult
{
    public List<DtoSetupOperationResult> Operations { get; set; } = new();

    /// <summary>True only when every attempted operation succeeded.</summary>
    public bool IsSuccess => Operations.All(o => o.IsSuccess);

    /// <summary>
    /// True when setup was marked complete and its state cleared. False whenever a required save failed, so the
    /// assistant keeps the answers and the user can retry.
    /// </summary>
    public bool IsSetupCompleted { get; set; }

    public IEnumerable<DtoSetupOperationResult> FailedOperations => Operations.Where(o => !o.IsSuccess);
}
