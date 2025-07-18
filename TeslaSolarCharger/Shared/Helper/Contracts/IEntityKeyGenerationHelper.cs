using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Shared.Helper.Contracts;

public interface IEntityKeyGenerationHelper
{
    string GetLoadPointEntityKey(int? carId, int? connectorId);
    DtoLoadpointCombination GetCombinationByKey(string entityKey);
    string GetDataKey(string dataType, string? entityId);
}
