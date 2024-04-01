using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;

public class DtoModbusConfiguration
{
    public int Id { get; set; }
    public decimal CorrectionFactor { get; set; }
    public ValueUsage UsedFor { get; set; }
    public ValueOperator Operator { get; set; }
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
}
