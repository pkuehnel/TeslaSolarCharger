using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

public class DtoModbusConfiguration
{
    public int Id { get; set; }
    public int UnitIdentifier { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public ModbusEndianess Endianess { get; set; }
    public int ConnectDelaySeconds { get; set; }
    public int ReadTimeoutSeconds { get; set; }
}
