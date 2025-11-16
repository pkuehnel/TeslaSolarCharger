using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services.ValueRefresh;

public interface IAutoRefreshingValue<T> : IGenericValue<T>
{
}


public class AutoRefreshingValue<T> : GenericValueBase<T>, IAutoRefreshingValue<T>
{

    public override SourceValueKey SourceValueKey { get; }

    public AutoRefreshingValue(SourceValueKey sourceValueKey, int historicValueCapacity, IServiceScopeFactory serviceScopeFactory)
        : base(serviceScopeFactory, historicValueCapacity)
    {
        SourceValueKey = sourceValueKey;
    }

    public override ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public override void Cancel()
    {
        throw new NotImplementedException();
    }
}
