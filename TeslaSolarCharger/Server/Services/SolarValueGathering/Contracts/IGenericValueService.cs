using System.Linq.Expressions;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;

public interface IGenericValueService
{
    List<IGenericValue<decimal>> GetAllByPredicate(Expression<Func<IGenericValue<decimal>, bool>> predicate);

    /// <summary>
    /// The current solar, grid and home battery values of every device, with everything one device reads for the same
    /// measurement added up into one entry.
    /// </summary>
    List<DtoPvSourceValue> GetSourceValues(bool skipValuesWithError);

    Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds);
}
