using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public class Issue
{
    public Issue()
    {
        IssueCode = string.Empty;
        IssueMessage = string.Empty;
    }

    public string IssueCode { get; set; }
    public string IssueMessage { get; set; }
    public IssueType? IssueType { get; set; }
}
