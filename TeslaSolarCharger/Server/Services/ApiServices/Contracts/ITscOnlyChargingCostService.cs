namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface ITscOnlyChargingCostService
{
    Task AddChargingDetailsForAllCars();
    Task FinalizeFinishedChargingProcesses();
    Task UpdateChargePricesOfAllChargingProcesses();
}
