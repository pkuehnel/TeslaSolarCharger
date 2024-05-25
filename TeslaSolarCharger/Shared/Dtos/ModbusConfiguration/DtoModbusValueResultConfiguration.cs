using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

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

    [HelperText("If you have an inverter that always displays positive values, you can use this to invert the value based on a bit. For now this is only known for the battery power of Sungrow inverters")]
    public int? InvertedByModbusResultConfigurationId { get; set; }
}
