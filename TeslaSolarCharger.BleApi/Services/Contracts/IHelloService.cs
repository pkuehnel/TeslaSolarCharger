namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IHelloService
{
    Task<bool> IsAlive();
}