using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;

public interface IDecimalValueHandlingService :
    IGenericValueHandlingService<decimal, int>
{
}

public abstract class DecimalValueHandlingServiceBase<TGenericValue> : GenericValueHandlingServiceBase<TGenericValue, decimal, int>
    where TGenericValue : IGenericValue<decimal>
{
    protected DecimalValueHandlingServiceBase(IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
    {
    }
}

public interface IGenericValueHandlingService<TValue, TConfigurationId>
{
    Task RecreateValues(ConfigurationType? configurationType, params List<TConfigurationId> configurationIds);
    List<IGenericValue<TValue>> GetSnapshot();
    IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<TValue>>> GetValuesByUsage(HashSet<ValueUsage> valueUsages,
        bool skipValuesWithError);
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

    public IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<TValue>>> GetValuesByUsage(HashSet<ValueUsage> valueUsages, bool skipValuesWithError)
    {
        var result = new Dictionary<ValueUsage, List<DtoHistoricValue<TValue>>>();

        var refreshablesSnapshot = GetGenericValuesSnapshot();

        foreach (var refreshable in refreshablesSnapshot)
        {
            if (skipValuesWithError && refreshable.HasError)
            {
                continue;
            }
            foreach (var (key, latestValue) in refreshable.HistoricValues)
            {
                if (key.ValueUsage == default || !valueUsages.Contains(key.ValueUsage.Value))
                {
                    continue;
                }

                result.TryAdd(key.ValueUsage.Value, new());
                result[key.ValueUsage.Value].Add(latestValue);
            }
        }

        return result;
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
