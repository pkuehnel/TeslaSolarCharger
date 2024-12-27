namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IFleetApiTokenCheckService
{
    Task<bool> HasValidBackendToken();
}
