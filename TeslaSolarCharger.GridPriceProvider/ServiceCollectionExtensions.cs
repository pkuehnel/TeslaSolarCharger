using GraphQL.Client.Abstractions;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.GridPriceProvider.Services;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

namespace TeslaSolarCharger.GridPriceProvider;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGridPriceProvider(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddTransient<IFixedPriceService, FixedPriceService>();

        return services;
    }
}
