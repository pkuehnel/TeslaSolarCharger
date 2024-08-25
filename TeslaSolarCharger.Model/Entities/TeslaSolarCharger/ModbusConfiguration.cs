using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ModbusConfiguration
{
    public int Id { get; set; }
    public int UnitIdentifier { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public ModbusEndianess Endianess { get; set; }
    public int ConnectDelayMilliseconds { get; set; }
    public int ReadTimeoutMilliseconds { get; set; }

    public List<ModbusResultConfiguration> ModbusResultConfigurations { get; set; }
}
