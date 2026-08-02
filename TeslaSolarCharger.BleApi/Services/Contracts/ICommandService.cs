using PkSoftwareService.Custom.Backend.Ble;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface ICommandService
{
    Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, string? domain, List<string> parameters,
        string? adapter, int? keepWarmSeconds);
    Task<DtoBleBeaconScanResult> BeaconScan(List<string> vins, string? adapter, int? keepWarmSeconds);
    Task<DtoBleCommandResult> ListCommands();
}
