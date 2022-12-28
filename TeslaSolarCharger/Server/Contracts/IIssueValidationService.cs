using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IIssueValidationService
{
    Task<List<Issue>> RefreshIssues();
    Task<DtoValue<int>> ErrorCount();
    Task<DtoValue<int>> WarningCount();
}
