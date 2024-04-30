using TeslaSolarCharger.Model.BaseClasses;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ModbusResultConfiguration : ResultConfigurationBase
{
    public ModbusRegisterType RegisterType { get; set; }
    public ModbusValueType ValueType { get; set; }
    public int Address { get; set; }
    public int Length { get; set; }
    public int? BitStartIndex { get; set; }

    public int ModbusConfigurationId { get; set; }
    public int? InvertedByModbusResultConfigurationId { get; set; }

    public ModbusConfiguration ModbusConfiguration { get; set; }
    public ModbusResultConfiguration? InvertedByModbusResultConfiguration { get; set; }
    
}
