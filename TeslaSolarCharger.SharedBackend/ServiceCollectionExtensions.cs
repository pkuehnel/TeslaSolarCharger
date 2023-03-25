using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.SharedBackend.Values;

namespace TeslaSolarCharger.SharedBackend;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedBackendDependencies(this IServiceCollection services)
        => services.AddTransient<IContstants, Contstants>()
        ;
}
