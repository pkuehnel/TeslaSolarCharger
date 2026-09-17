using System.Linq.Expressions;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering;

public class GenericValueService : IGenericValueService
{
    private readonly ILogger<GenericValueService> _logger;
    private readonly IEnumerable<IDecimalValueHandlingService> _decimalValueHandlingServices;

    public GenericValueService(ILogger<GenericValueService> logger,
        IEnumerable<IDecimalValueHandlingService> decimalValueHandlingServices)
    {
        _logger = logger;
        _decimalValueHandlingServices = decimalValueHandlingServices;
    }

    public List<IGenericValue<decimal>> GetAllByPredicate(Expression<Func<IGenericValue<decimal>, bool>> predicate)
    {
        var elements = new List<IGenericValue<decimal>>();
        foreach (var service in _decimalValueHandlingServices)
        {
            var snapshot = service.GetSnapshot();
            var filtered = snapshot.AsQueryable().Where(predicate).ToList();
            elements.AddRange(filtered);
        }
        return elements;
    }

    public List<DtoPvSourceValue> GetSourceValues(bool skipValuesWithError)
    {
        var result = new List<DtoPvSourceValue>();
        foreach (var genericValue in GetAllByPredicate(v => !skipValuesWithError || !v.HasError))
        {
            var valuesByUsage = genericValue.HistoricValues
                .Where(v => v.Key.ValueUsage != default)
                .GroupBy(v => v.Key.ValueUsage!.Value);
            foreach (var usageValues in valuesByUsage)
            {
                result.Add(new DtoPvSourceValue
                {
                    ConfigurationType = genericValue.SourceValueKey.ConfigurationType,
                    SourceId = genericValue.SourceValueKey.SourceId,
                    UsedFor = usageValues.Key,
                    //A group always holds at least one value, so there always is a sum.
                    Value = usageValues.Select(v => v.Value).SumWithNewestTimestamp()!,
                });
            }
        }

        return result;
    }

    public async Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds)
    {
        _logger.LogTrace("{method}({configurationType}, {@configurationIds})", nameof(RecreateValues), configurationType, configurationIds);
        foreach (var service in _decimalValueHandlingServices)
        {
            _logger.LogTrace("Recreate values for type {typeName}", service.GetType().Name);
            await service.RecreateValues(configurationType, configurationIds);
        }
    }
}
