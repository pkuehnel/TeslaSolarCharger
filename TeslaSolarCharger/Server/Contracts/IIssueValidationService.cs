using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IIssueValidationService
{
    Task<List<Issue>> RefreshIssues(TimeSpan clientTimeZoneId);
    Task<DtoValue<int>> ErrorCount();
    Task<DtoValue<int>> WarningCount();
}
