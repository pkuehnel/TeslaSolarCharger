using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

public class DtoModbusConfiguration
{
    public int Id { get; set; }
    [Required]
    [Range(byte.MinValue, byte.MaxValue)]
    public int? UnitIdentifier { get; set; }
    public string Host { get; set; }
    [Range(0, 65535)]
    public int Port { get; set; } = 502;
    public ModbusEndianess Endianess { get; set; }
    [Range(0, 10000)]
    [Postfix("ms")]
    public int ConnectDelayMilliseconds { get; set; }
    [Range(1000, 10000)]
    [Postfix("ms")]
    public int ReadTimeoutMilliseconds { get; set; } = 1000;
}
