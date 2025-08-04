namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IEnergyDataService
{
    Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date, CancellationToken token);
    Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date, CancellationToken token);
    Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date, CancellationToken token);
    Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date, CancellationToken token);
    Task<bool> SolarPowerPredictionEnabled();
    Task<bool> ShowEnergyDataOnHome();

    Task<Dictionary<int, int>> GetActualHomeBatteryChargingPowerByLocalHour(
        DateOnly date, CancellationToken token);

    Task<Dictionary<int, int>> GetActualHomeBatteryDischargingPowerByLocalHour(
        DateOnly date, CancellationToken token);

    Task<Dictionary<int, int>> GetActualPowerToGridByLocalHour(
        DateOnly date, CancellationToken token);

    Task<Dictionary<int, int>> GetActualPowerFromGridByLocalHour(
        DateOnly date, CancellationToken token);

    Task<Dictionary<int, int>> GetActualHomeBatterySocByLocalHour(
        DateOnly date, CancellationToken token);
}
