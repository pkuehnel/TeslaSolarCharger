using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IPossibleIssues
{
    DtoIssue GetIssueByKey(string key);
}
