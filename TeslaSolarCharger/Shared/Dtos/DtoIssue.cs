using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoIssue
{
    public DtoIssue()
    {
        IssueMessage = string.Empty;
    }

    public string IssueMessage { get; set; }
    public List<string> PossibleSolutions { get; set; } = new() {};
    public IssueType? IssueType { get; set; }
}
