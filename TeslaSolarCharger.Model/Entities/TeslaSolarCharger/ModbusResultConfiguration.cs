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
    public int? BitLength { get; set; }

    public int ModbusConfigurationId { get; set; }
    public int? InvertsModbusResultConfigurationId { get; set; }

    public ModbusConfiguration ModbusConfiguration { get; set; }
    public ModbusResultConfiguration? InvertsModbusResultConfiguration { get; set; }
    
}
