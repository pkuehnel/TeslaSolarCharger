namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IEnergyPredictionService
{
    Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date);
    Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date);
}
