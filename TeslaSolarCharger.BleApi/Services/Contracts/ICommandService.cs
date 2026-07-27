using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface ICommandService
{
    Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, string? domain, List<string> parameters);
    Task<DtoBleCommandResult> BeaconScan(string vin);
    Task<DtoBleCommandResult> ListCommands();
}