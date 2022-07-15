namespace TeslaSolarCharger.Server.Contracts;

public interface IGridService
{
    Task<int?> GetCurrentOverage();
    Task<int?> GetCurrentInverterPower();
}