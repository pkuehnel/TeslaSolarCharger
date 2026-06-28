using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface ICommandLineExecutionService
{
    Task<DtoBleCommandResult> ExecuteCommand(string filename, string parameters);
}