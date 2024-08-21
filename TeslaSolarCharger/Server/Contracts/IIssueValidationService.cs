using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IIssueValidationService
{
    Task<List<DtoIssue>> RefreshIssues(TimeSpan clientTimeZoneId);
    Task<DtoValue<int>> ErrorCount();
    Task<DtoValue<int>> WarningCount();
}
