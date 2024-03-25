using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedDependencies(this IServiceCollection services) =>
        services
            .AddTransient<IStringHelper, StringHelper>()
            .AddTransient<IConstants, Constants>()
        ;
}
