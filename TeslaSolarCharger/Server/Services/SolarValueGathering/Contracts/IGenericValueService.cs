using System.Linq.Expressions;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;

public interface IGenericValueService
{
    List<IGenericValue<decimal>> GetAllByPredicate(Expression<Func<IGenericValue<decimal>, bool>> predicate);

    Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds);
}
