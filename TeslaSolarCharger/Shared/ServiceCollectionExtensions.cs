using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedDependencies(this IServiceCollection services) =>
        services
            .AddLocalization(options => options.ResourcesPath = "Localization/Resources")
            .AddScoped<IAppLocalizationService, AppLocalizationService>()
            .AddTransient<IStringHelper, StringHelper>()
            .AddTransient<IConstants, Constants>()
            .AddTransient<IValidFromToHelper, ValidFromToHelper>()
        ;
}
