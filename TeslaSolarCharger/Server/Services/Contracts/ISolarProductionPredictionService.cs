namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISolarProductionPredictionService
{
    Task<Dictionary<DateTimeOffset, double>> GetPredictedSolarProductionByLocalHour(DateOnly date);
}
