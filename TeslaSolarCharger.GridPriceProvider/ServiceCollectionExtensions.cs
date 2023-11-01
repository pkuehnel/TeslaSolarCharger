using GraphQL.Client.Abstractions;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TeslaSolarCharger.GridPriceProvider.Data.Enums;
using TeslaSolarCharger.GridPriceProvider.Data.Options;
using TeslaSolarCharger.GridPriceProvider.Services;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

namespace TeslaSolarCharger.GridPriceProvider;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGridPriceProvider(this IServiceCollection services, ConfigurationManager configurationManager)
    {
        services.AddHttpClient();
        var energyProvider = configurationManager.GetValue("GridPriceProvider:EnergyProvider", EnergyProvider.Octopus);
        if (energyProvider == EnergyProvider.Octopus)
        {
            services.AddOptions<OctopusOptions>()
                .Bind(configurationManager.GetSection("Octopus"))
                .ValidateDataAnnotations();
            services.AddHttpClient<IPriceDataService, OctopusService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OctopusOptions>>().Value;
                var baseUrl = options.BaseUrl;
                if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                client.BaseAddress = new Uri(baseUrl);
            });
        }
        else if (energyProvider == EnergyProvider.Tibber)
        {
            services.AddOptions<TibberOptions>()
                .Bind(configurationManager.GetSection("Tibber"))
                .ValidateDataAnnotations();
            services.AddTransient<IGraphQLJsonSerializer, SystemTextJsonSerializer>();
            services.AddHttpClient<IPriceDataService, TibberService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<TibberOptions>>().Value;
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
            });
        }
        else if (energyProvider == EnergyProvider.FixedPrice)
        {
            services.AddOptions<FixedPriceOptions>()
               .Bind(configurationManager.GetSection("FixedPrice"))
               .ValidateDataAnnotations();
            services.AddSingleton<IPriceDataService, FixedPriceService>();
        }
        else if (energyProvider == EnergyProvider.Awattar)
        {
            services.AddOptions<AwattarOptions>()
                .Bind(configurationManager.GetSection("Awattar"))
                .ValidateDataAnnotations();
            services.AddHttpClient<IPriceDataService, AwattarService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AwattarOptions>>().Value;
                var baseUrl = options.BaseUrl;
                if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                client.BaseAddress = new Uri(baseUrl);
            });
        }
        else if (energyProvider == EnergyProvider.Energinet)
        {
            services.AddOptions<EnerginetOptions>()
                .Bind(configurationManager.GetSection("Energinet"))
                .ValidateDataAnnotations();
            services.AddHttpClient<IPriceDataService, EnerginetService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<EnerginetOptions>>().Value;
                var baseUrl = options.BaseUrl;
                if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                client.BaseAddress = new Uri(baseUrl);
            });
        }
        else if (energyProvider == EnergyProvider.HomeAssistant)
        {
            services.AddOptions<HomeAssistantOptions>()
                .Bind(configurationManager.GetSection("HomeAssistant"))
                .ValidateDataAnnotations();
            services.AddHttpClient<IPriceDataService, HomeAssistantService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<HomeAssistantOptions>>().Value;
                var baseUrl = options.BaseUrl;
                if (!baseUrl.EndsWith("/")) { baseUrl += "/"; }
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
            });
        }
        else
        {
            throw new ArgumentException("Invalid energy provider set", nameof(energyProvider));
        }
        return services;
    }
}
