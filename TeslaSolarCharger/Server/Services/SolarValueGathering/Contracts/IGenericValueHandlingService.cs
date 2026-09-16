using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;

public interface IDecimalValueHandlingService :
    IGenericValueHandlingService<decimal, int>
{
    /// <summary>
    /// The current value of every device for each of <paramref name="valueUsages"/>, with everything one device reads
    /// for the same usage added up into one entry.
    /// </summary>
    List<DtoPvSourceValue> GetSourceValues(HashSet<ValueUsage> valueUsages, bool skipValuesWithError);
}

public abstract class DecimalValueHandlingServiceBase<TGenericValue> : GenericValueHandlingServiceBase<TGenericValue, decimal, int>,
    IDecimalValueHandlingService
    where TGenericValue : IGenericValue<decimal>
{
    protected DecimalValueHandlingServiceBase(IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
    {
    }

    public List<DtoPvSourceValue> GetSourceValues(HashSet<ValueUsage> valueUsages, bool skipValuesWithError)
    {
        var result = new List<DtoPvSourceValue>();

        foreach (var genericValue in GetGenericValuesSnapshot())
        {
            if (skipValuesWithError && genericValue.HasError)
            {
                continue;
            }

            var valuesByUsage = genericValue.HistoricValues
                .Where(v => v.Key.ValueUsage != default && valueUsages.Contains(v.Key.ValueUsage.Value))
                .GroupBy(v => v.Key.ValueUsage!.Value);
            foreach (var usageValues in valuesByUsage)
            {
                result.Add(new DtoPvSourceValue
                {
                    ConfigurationType = genericValue.SourceValueKey.ConfigurationType,
                    SourceId = genericValue.SourceValueKey.SourceId,
                    UsedFor = usageValues.Key,
                    Value = usageValues.Sum(v => v.Value.Value),
                    LastUpdated = usageValues.Max(v => v.Value.Timestamp),
                });
            }
        }

        return result;
    }
}

public interface IGenericValueHandlingService<TValue, TConfigurationId>
{
    Task RecreateValues(ConfigurationType? configurationType, params List<TConfigurationId> configurationIds);
    List<IGenericValue<TValue>> GetSnapshot();
}

public abstract class
    GenericValueHandlingServiceBase<TGenericValue, TValue, TConfgigurationId> : IGenericValueHandlingService<TValue, TConfgigurationId>
    where TGenericValue : IGenericValue<TValue>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HashSet<TGenericValue> _values = new();
    private readonly object _valuesLock = new();

    protected GenericValueHandlingServiceBase(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public abstract Task RecreateValues(ConfigurationType? configurationType, params List<TConfgigurationId> configurationIds);

    public List<IGenericValue<TValue>> GetSnapshot()
    {
        return GetGenericValuesSnapshot()
            .Cast<IGenericValue<TValue>>()
            .ToList();
    }

    protected List<TGenericValue> GetGenericValuesSnapshot()
    {
        lock (_valuesLock)
        {
            return _values.ToList();
        }
    }

    protected void AddGenericValues(IEnumerable<TGenericValue> valuesToAdd)
    {
        lock (_valuesLock)
        {
            foreach (var valueToAdd in valuesToAdd)
            {
                _values.Add(valueToAdd);
            }
        }
    }

    protected async Task RemoveValuesAsync(IEnumerable<TGenericValue> valuesToRemove)
    {
        var values = valuesToRemove.ToList();

        var disposals = values
            .Select(v => (item: v, disposeTask: v.DisposeAsync().AsTask()))
            .ToList();

        var successfullyDisposed = new List<TGenericValue>();

        foreach (var (item, disposeTask) in disposals)
        {
            try
            {
                await disposeTask.ConfigureAwait(false);
                successfullyDisposed.Add(item);
            }
            catch (Exception ex)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<GenericValueHandlingServiceBase<TGenericValue, TValue, TConfgigurationId>>>();
                logger.LogError(ex, "Error disposing value of type {Type}", typeof(TGenericValue).FullName);
            }
        }

        lock (_valuesLock)
        {
            foreach (var item in successfullyDisposed)
            {
                _values.Remove(item);
            }
        }
    }
}
