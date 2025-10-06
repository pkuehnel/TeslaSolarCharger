namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IBaseConfigurationService
{
    Task<bool> HomeBatteryValuesAvailable();
}
