namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISolarProductionPredictionService
{
    Task<Dictionary<int, int>> GetPredictedProductionByLocalHour(DateOnly date);
}
