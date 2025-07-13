using TeslaSolarCharger.Shared.Helper.Contracts;

namespace TeslaSolarCharger.Shared.Helper;

public class EntityKeyGenerationHelper : IEntityKeyGenerationHelper
{
    public string GetLoadPointEntityKey(int? carId, int? connectorId)
    {
        return $"{carId}_{connectorId}";
    }
}
