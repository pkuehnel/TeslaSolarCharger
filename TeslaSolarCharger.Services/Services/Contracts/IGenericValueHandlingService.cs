using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services.Contracts;

public interface IGenericValueHandlingService
{
    List<IGenericValue<decimal>> GetSnapshot();
}
