using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.SharedBackend;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedBackendDependencies(this IServiceCollection services)
        => services
        ;
}
