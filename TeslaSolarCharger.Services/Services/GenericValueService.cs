using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services;

public class GenericValueService : IGenericValueService
{
    private readonly ILogger<GenericValueService> _logger;
    private readonly IEnumerable<IGenericValueHandlingService> _genericValueHandlingServices;

    public GenericValueService(ILogger<GenericValueService> logger,
        IEnumerable<IGenericValueHandlingService> genericValueHandlingServices)
    {
        _logger = logger;
        _genericValueHandlingServices = genericValueHandlingServices;
    }

    public List<IGenericValue<decimal>> GetAllByPredicate(Expression<Func<IGenericValue<decimal>, bool>> predicate)
    {
        var elements = new List<IGenericValue<decimal>>();
        foreach (var service in _genericValueHandlingServices)
        {
            var snapshot = service.GetSnapshot();
            var filtered = snapshot.AsQueryable().Where(predicate).ToList();
            elements.AddRange(filtered);
        }
        return elements;
    }
}
