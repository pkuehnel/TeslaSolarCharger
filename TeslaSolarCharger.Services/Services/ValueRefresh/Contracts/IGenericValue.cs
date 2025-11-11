using System.Collections.Concurrent;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

/// <summary>
/// Dictionary of Historic Values for a generic type T with ValueKey as key
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IGenericValue <T>
{
    SourceValueKey SourceValueKey { get; }
    IReadOnlyDictionary<ValueKey, DtoHistoricValue<T>> HistoricValues { get; }
    void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, T? value);
}

/// <summary>
/// Key to identify a Generic value
/// </summary>
/// <param name="ValueUsage"></param>
/// <param name="CarValueType"></param>
public sealed record ValueKey(
    ValueUsage? ValueUsage,
    CarValueType? CarValueType,
    int ResultConfigurationId
);

/// <summary>
/// Key to identify a Source
/// </summary>
/// <param name="SourceId">This can either be a configuration ID like rest value configuration ID or a car ID</param>
/// <param name="ConfigurationType"></param>
public sealed record SourceValueKey(
    int SourceId,
    ConfigurationType ConfigurationType
);


public enum ConfigurationType
{
    RestSolarValue,
    ModbusSolarValue,
    MqttSolarValue,
    CarValue,
    OcppChargingConnectorValue,
    TemplateValue = 1000,
}
