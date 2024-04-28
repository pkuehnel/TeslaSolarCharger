using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

public class DtoModbusConfiguration
{
    public int Id { get; set; }
    [Required]
    public int? UnitIdentifier { get; set; }
    public string Host { get; set; }
    [Range(0, 65535)]
    public int Port { get; set; } = 502;
    public ModbusEndianess Endianess { get; set; }
    [Range(0, 10)]
    public int ConnectDelaySeconds { get; set; }
    [Range(1, 10)]
    public int ReadTimeoutSeconds { get; set; } = 1;
}
