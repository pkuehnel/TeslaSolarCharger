namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IEnergyDataService
{
    Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date);
    Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date);
    Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date);
    Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date);
    Task<bool> SolarPowerPredictionEnabled();
}
