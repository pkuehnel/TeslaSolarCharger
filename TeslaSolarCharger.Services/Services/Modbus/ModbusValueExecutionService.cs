using Microsoft.Extensions.Logging;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services.Modbus;

public class ModbusValueExecutionService(ILogger<ModbusValueExecutionService> logger,
    IModbusValueConfigurationService modbusValueConfigurationService,
    IModbusClientHandlingService modbusClientHandlingService,
    IResultValueCalculationService resultValueCalculationService) : IModbusValueExecutionService
{
    public async Task<byte[]> GetResult(DtoModbusConfiguration modbusConfig, DtoModbusValueResultConfiguration resultConfiguration, bool ignoreBackoff)
    {
        logger.LogTrace("{method}({modbusConfig}, {resultConfiguration}, {ignoreBackoff})", nameof(GetResult), modbusConfig, resultConfiguration, ignoreBackoff);
        var byteArray = await modbusClientHandlingService.GetByteArray((byte)modbusConfig.UnitIdentifier!, modbusConfig.Host,
            modbusConfig.Port, modbusConfig.Endianess, TimeSpan.FromMilliseconds(modbusConfig.ConnectDelayMilliseconds),
            TimeSpan.FromMilliseconds(modbusConfig.ReadTimeoutMilliseconds), resultConfiguration.RegisterType,
            (ushort)resultConfiguration.Address, (ushort)resultConfiguration.Length, ignoreBackoff);
        return byteArray;
    }

    public async Task<decimal> GetValue(byte[] byteArray, DtoModbusValueResultConfiguration resultConfig)
    {
        logger.LogTrace("{method}({byteArray}, {resultConfig})", nameof(GetValue), byteArray, resultConfig);
        decimal rawValue;
        switch (resultConfig.ValueType)
        {
            case ModbusValueType.Int:
                var value = BitConverter.ToInt32(byteArray, 0);
                rawValue = value;
                break;
            case ModbusValueType.Float:
                var floatValue = BitConverter.ToSingle(byteArray, 0);
                rawValue = (decimal)floatValue;
                break;
            case ModbusValueType.Short:
                var shortValue = BitConverter.ToInt16(byteArray, 0);
                rawValue = shortValue;
                break;
            case ModbusValueType.UInt:
                var uintValue = BitConverter.ToUInt32(byteArray, 0);
                rawValue = uintValue;
                break;
            case ModbusValueType.UShort:
                var ushortValue = BitConverter.ToUInt16(byteArray, 0);
                rawValue = ushortValue;
                break;
            case ModbusValueType.Ulong:
                var ulongValue = BitConverter.ToUInt64(byteArray, 0);
                rawValue = ulongValue;
                break;
            case ModbusValueType.Bool:
                if (resultConfig.BitStartIndex == null)
                    throw new ArgumentException("BitStartIndex must be set for ValueType Bool", nameof(ModbusResultConfiguration.BitStartIndex));
                var binaryString = GetBinaryString(byteArray);
                var bitChar = binaryString[resultConfig.BitStartIndex.Value];
                logger.LogDebug("Bit value: {bitChar}", bitChar);
                rawValue = bitChar == '1' ? 1 : 0;
                return rawValue;
            default:
                throw new ArgumentOutOfRangeException();
        }

        rawValue = await InvertValueOnExistingInversionRegister(rawValue, resultConfig.InvertedByModbusResultConfigurationId, false);
        return resultValueCalculationService.MakeCalculationsOnRawValue(resultConfig.CorrectionFactor, resultConfig.Operator, rawValue);
    }

    private async Task<decimal> InvertValueOnExistingInversionRegister(decimal rawValue, int? resultConfigInvertedByModbusResultConfigurationId, bool ignoreBackoff)
    {
        logger.LogTrace("{method}({rawValue}, {resultConfigInvertedByModbusResultConfigurationId}, {ignoreBackoff})", nameof(InvertValueOnExistingInversionRegister), rawValue, resultConfigInvertedByModbusResultConfigurationId, ignoreBackoff);
        if (resultConfigInvertedByModbusResultConfigurationId == default)
        {
            return rawValue;
        }

        var resultConfigurations =
            await modbusValueConfigurationService.GetModbusResultConfigurationsByPredicate(r =>
                r.Id == resultConfigInvertedByModbusResultConfigurationId);
        var resultConfiguration = resultConfigurations.Single();
        var valueConfigurations = await modbusValueConfigurationService.GetModbusConfigurationByPredicate(c =>
            c.ModbusResultConfigurations.Any(r => r.Id == resultConfigInvertedByModbusResultConfigurationId.Value));
        var valueConfiguration = valueConfigurations.Single();
        var byteArray = await GetResult(valueConfiguration, resultConfiguration, ignoreBackoff);
        logger.LogDebug("Inversion bits: {byteArray}", GetBinaryString(byteArray));
        var inversionValue = await GetValue(byteArray, resultConfiguration);
        logger.LogDebug("Inversion value: {inversionValue}", inversionValue);
        return inversionValue == 0 ? rawValue : -rawValue;
    }

    public string GetBinaryString(byte[] byteArray)
    {
        var stringbuilder = new StringBuilder();
        foreach (var byteValue in byteArray)
        {
            stringbuilder.Append(Convert.ToString(byteValue, 2).PadLeft(8, '0'));
        }

        return stringbuilder.ToString();
    }
}
