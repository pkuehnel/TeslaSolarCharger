namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueImportService
{
    Task ImportMeterValuesFromChargingDetailsAsync();
}
