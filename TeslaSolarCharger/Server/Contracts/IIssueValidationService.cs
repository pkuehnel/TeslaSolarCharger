using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IIssueValidationService
{
    Task<List<Issue>> RefreshIssues();
    Task<int> ErrorCount();
    Task<int> WarningCount();
}
