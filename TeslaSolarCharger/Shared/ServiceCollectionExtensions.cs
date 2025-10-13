using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using TeslaSolarCharger.Shared.Localization.Registries.Pages;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedDependencies(this IServiceCollection services) =>
        services
            .AddTransient<IStringHelper, StringHelper>()
            .AddTransient<IConstants, Constants>()
            .AddTransient<IValidFromToHelper, ValidFromToHelper>()
            .AddSingleton<IPropertyLocalizationService, PropertyLocalizationService>()
            .AddSingleton<IPropertyLocalizationRegistry, BaseConfigurationBasePropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, CarBasicConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, ChargingStationConnectorPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, CarOverviewSettingsPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, CarChargingTargetPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, ModbusValueResultConfigurationPropertyLocalization>()
            .AddSingleton<ITextLocalizationService, TextLocalizationService>()
            .AddSingleton<ITextLocalizationRegistry, SharedComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, BaseConfigurationPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, CarSettingsPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargeCostDetailPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargeCostsListPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, CloudConnectionPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingStationsPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, HandledChargesListPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, HomePageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, FixedPriceComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingStationConnectorsComponentLocalizationRegistry>()
        ;
}
