using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Contracts;

public interface IPossibleIssues
{
    Issue GetIssueByKey(string key);
}
