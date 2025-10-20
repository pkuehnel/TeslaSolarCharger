using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

public class DtoModbusValueResultConfiguration : ValueConfigurationBase
{
    [Required]
    public ModbusRegisterType RegisterType { get; set; }
    [Required]
    public ModbusValueType ValueType { get; set; }
    [Required]
    [Range(ushort.MinValue, ushort.MaxValue)]
    public int Address { get; set; }
    [Required]
    [Range(ushort.MinValue, ushort.MaxValue)]
    public int Length { get; set; }
    public int? BitStartIndex { get; set; }
    public int? InvertedByModbusResultConfigurationId { get; set; }
}
