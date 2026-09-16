using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// A setting the app can decide on its own, together with why. Proposals are only made for values the user has not
/// explicitly decided, and are shown before they are applied.
/// </summary>
public class DtoSetupProposedValue
{
    /// <summary>Name of the configuration property the proposal applies to.</summary>
    public string PropertyName { get; set; } = string.Empty;

    public object? Value { get; set; }

    /// <summary>Translation key explaining the proposal in ordinary language.</summary>
    public string ReasonKey { get; set; } = string.Empty;

    /// <summary>Draft the proposal applies to, null for base configuration properties.</summary>
    public Guid? DraftId { get; set; }

    /// <summary>
    /// True when the proposal cannot take effect yet because something it depends on is unknown. The choice stays
    /// visible and marked pending instead of being silently turned back into a manual setting.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>Issues that keep <see cref="IsPending"/> true.</summary>
    public List<DtoSetupIssue> PendingReasons { get; set; } = new();

    public SetupValueSource Source { get; set; } = SetupValueSource.DerivedFromAnswers;
}
