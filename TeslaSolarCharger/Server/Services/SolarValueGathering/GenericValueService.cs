using System.Linq.Expressions;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

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
