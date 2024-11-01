using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.LoggedError;

public class DtoLoggedError
{
    public int Id { get; set; }
    public IssueSeverity Severity { get; set; }
    public string Headline { get; set; }
    public string IssueKey { get; set; }
    public int OccurrenceCount { get; set; }
    public string? Vin { get; set; }
    public string Message { get; set; }
    public bool HideOccurrenceCount { get; set; }
}
