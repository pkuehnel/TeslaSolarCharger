using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoIssue
{
    public IssueSeverity IssueSeverity { get; set; } = IssueSeverity.Error;
    public bool IsTelegramEnabled { get; set; }
    public int ShowErrorAfterOccurrences { get; set; } = 1;
    /// <summary>
    /// If true the issue Starts with the specified issue key and can have multiple variations separated from the main issue key by _
    /// </summary>
    public bool HasPlaceHolderIssueKey { get; set; }
    public bool HideOccurrenceCount { get; set; }
}
