namespace TeslaSolarCharger.Server.Contracts;

public interface IGridService
{
    Task<int?> GetCurrentOverage(HttpResponseMessage response);
    Task<int?> GetCurrentInverterPower(HttpResponseMessage response);
}