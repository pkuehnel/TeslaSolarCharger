namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISolarProductionPredictionService
{
    Task<Dictionary<int, double>> GetPredictedSolarProductionByLocalHour(DateOnly date);
    Task<Dictionary<int, double>> GetPredictedHouseConsumptionByLocalHour(DateOnly date);
}
