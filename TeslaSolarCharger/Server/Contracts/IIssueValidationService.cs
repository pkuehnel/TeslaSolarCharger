using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IIssueValidationService
{
    List<Issue> RefreshIssues();
}
