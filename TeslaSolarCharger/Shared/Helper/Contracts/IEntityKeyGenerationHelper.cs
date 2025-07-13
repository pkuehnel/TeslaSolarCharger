namespace TeslaSolarCharger.Shared.Helper.Contracts;

public interface IEntityKeyGenerationHelper
{
    string GetLoadPointEntityKey(int? carId, int? connectorId);
}
