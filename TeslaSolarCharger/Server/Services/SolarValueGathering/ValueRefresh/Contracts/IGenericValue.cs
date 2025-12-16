using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

/// <summary>
/// Dictionary of Historic Values for a generic type T with ValueKey as key
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IGenericValue <T> : IAsyncDisposable
{
    SourceValueKey SourceValueKey { get; }
    IReadOnlyDictionary<ValueKey, DtoHistoricValue<T>> HistoricValues { get; }
    void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, T? value);
    string? ErrorMessage { get; }
    string? ErrorStackTrace { get; }
    DateTimeOffset? HasErrorSince { get; }
    bool HasError { get; }
    void Cancel();
}

public abstract class GenericValueBase<T> : IGenericValue<T>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private string? _errorMessage;
    private readonly ConcurrentDictionary<ValueKey, DtoHistoricValue<T>> _historicValues = new();
    private readonly int _historicValueCapacity;

    protected GenericValueBase(IServiceScopeFactory serviceScopeFactory, int historicValueCapacity)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _historicValueCapacity = historicValueCapacity;
    }

    public void UpdateValue(ValueKey valueKey, DateTimeOffset timestamp, T? value)
    {
        var exists = _historicValues.TryGetValue(valueKey, out var historicValue);
        if (exists)
        {
            historicValue?.Update(timestamp, value);
        }
        else
        {
            historicValue = new(timestamp, value, _historicValueCapacity);
            _historicValues.TryAdd(valueKey, historicValue);
        }
    }


    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            _errorMessage = value;
            if (string.IsNullOrEmpty(value))
            {
                HasErrorSince = null;
                ErrorStackTrace = null;
                return;
            }
            using var scope = _serviceScopeFactory.CreateScope();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            HasErrorSince = dateTimeProvider.DateTimeOffSetUtcNow();
        }
    }

    public IReadOnlyDictionary<ValueKey, DtoHistoricValue<T>> HistoricValues
    {
        get
        {
            return new ReadOnlyDictionary<ValueKey, DtoHistoricValue<T>>(_historicValues);
        }
    }

    protected void SetErrorFromException(Exception exception)
    {
        ErrorMessage = exception.Message;
        ErrorStackTrace = exception.StackTrace;
    }

    public string? ErrorStackTrace { get; private set; }

    public DateTimeOffset? HasErrorSince { get; private set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public abstract SourceValueKey SourceValueKey { get; }

    public abstract ValueTask DisposeAsync();
    public abstract void Cancel();
}


/// <summary>
/// Key to identify a Generic value
/// </summary>
/// <param name="ValueUsage"></param>
/// <param name="CarValueType"></param>
/// <param name="ResultConfigurationId"></param>
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
