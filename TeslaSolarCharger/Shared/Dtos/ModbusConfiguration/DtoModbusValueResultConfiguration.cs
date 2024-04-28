using System.ComponentModel.DataAnnotations;
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
    public int Address { get; set; }
    [Required]
    public int Length { get; set; }
    public int? BitStartIndex { get; set; }
    public int? BitLength { get; set; }

    public int? InvertsModbusResultConfigurationId { get; set; }
}
