using TeslaSolarCharger.Model.BaseClasses;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ModbusConfiguration : ResultConfigurationBase
{
    public int UnitIdentifier { get; set; }
    public ModbusRegisterType RegisterType { get; set; }
    public ModbusValueType ValueType { get; set; }
    public int Address { get; set; }
    public int Length { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public ModbusEndianess Endianess { get; set; }
    public int ConnectDelaySeconds { get; set; }
    public int ReadTimeoutSeconds { get; set; }
    public int? BitStartIndex { get; set; }
    public int? BitLength { get; set; }

    public int? InvertsModbusConfigurationId { get; set; }

    public ModbusConfiguration? InvertsModbusConfiguration { get; set; }
}
