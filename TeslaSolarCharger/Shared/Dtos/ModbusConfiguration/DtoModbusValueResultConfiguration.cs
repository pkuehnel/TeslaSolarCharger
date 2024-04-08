using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

public class DtoModbusValueResultConfiguration : ValueConfigurationBase
{
    public ModbusRegisterType RegisterType { get; set; }
    public ModbusValueType ValueType { get; set; }
    public int Address { get; set; }
    public int Length { get; set; }
    public int? BitStartIndex { get; set; }
    public int? BitLength { get; set; }

    public int? InvertsModbusResultConfigurationId { get; set; }
}
