using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface ICommandService
{
    Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, string? domain, List<string> parameters, bool useDebug);
    Task<DtoBleCommandResult> BeaconScan(string vin, bool useDebug);
    Task<DtoBleCommandResult> ListCommands();
}