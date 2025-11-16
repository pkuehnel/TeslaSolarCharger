using System.Linq.Expressions;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IGenericValueService
{
    List<IGenericValue<decimal>> GetAllByPredicate(Expression<Func<IGenericValue<decimal>, bool>> predicate);

    Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds);
}
