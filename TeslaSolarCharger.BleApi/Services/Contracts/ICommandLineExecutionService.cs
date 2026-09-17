using PkSoftwareService.Custom.Backend.Ble;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface ICommandLineExecutionService
{
    Task<DtoBleCommandResult> ExecuteCommand(string filename, string parameters);
}
