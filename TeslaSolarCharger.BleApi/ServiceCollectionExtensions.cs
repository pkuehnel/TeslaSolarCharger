using TeslaSolarCharger.BleApi.InMemoryValues;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceDependencies(this IServiceCollection services)
    {
        services.AddHttpClient();
        return services
            .AddTransient<ICommandLineExecutionService, CommandLineExecutionService>()
            .AddTransient<IPairingService, PairingService>()
            .AddTransient<IHelloService, HelloService>()
            .AddSingleton<ICommandService, CommandService>()
            .AddSingleton<ISettings, Settings>()
            .AddSingleton<IStartupService, StartupService>()
            .AddSingleton(TimeProvider.System)
        ;
    }
}