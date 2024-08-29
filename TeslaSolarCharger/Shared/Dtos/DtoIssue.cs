using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoIssue
{
    public string? IssueMessage { get; set; }
    public List<string> PossibleSolutions { get; set; } = new() {};
    public IssueSeverity? IssueSeverity { get; set; }
    public IssueType? IssueType { get; set; }
}
