using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Localization;
using TeslaSolarCharger.Shared.Localization.Contracts;
using TeslaSolarCharger.Shared.Localization.Registries;
using TeslaSolarCharger.Shared.Localization.Registries.Components;
using TeslaSolarCharger.Shared.Localization.Registries.Components.StartPage;
using TeslaSolarCharger.Shared.Localization.Registries.Pages;
using TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries;
using TeslaSolarCharger.Shared.Localization.Registries.PropertyRegistries.TemplateDtos;
using TeslaSolarCharger.Shared.Localization.Registries.Server;
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
            .AddSingleton<IPropertyLocalizationRegistry, ModbusConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, ModbusValueResultConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, MqttConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, MqttResultConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, RestValueConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, RestValueResultConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, ChargePricePropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, TemplateValueConfigurationBasePropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, DtoKostalModbusConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, DtoSmaEnergyMeterTemplateValueConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, DtoSmaInverterTemplateValueConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, DtoSolaxTemplateValueConfigurationPropertyLocalization>()
            .AddSingleton<IPropertyLocalizationRegistry, DtoTeslaPowerwallTemplateValueConfigurationPropertyLocalization>()
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
            .AddSingleton<ITextLocalizationRegistry, SupportPageLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, FixedPriceComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, AutoReloadOnVersionChangeComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, BackupComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingStationConnectorsComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, NavMenuComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, CustomIconLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, CarDetailsComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ValueSourceConfigurationLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, GenericValueConfigurationComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, BackendInformationDisplayComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargeSummaryComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingConnectorDetailsComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingSchedulesChartComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingSchedulesComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, EditFormComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ChargingTargetConfigurationComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, EnergyPredictionComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, FleetApiTestComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, HiddenErrorsComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, LoadpointComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, LoggedErrorsComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, ManualOcppChargingComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, NotChargingAtExpectedPowerReasonsComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, NotChargingWithExpectedPowerReasonLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, PowerBufferComponentLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, InstallationInformationLocalizationRegistry>()
            .AddSingleton<ITextLocalizationRegistry, MerryChristmasAndHappyNewYearComponentLocalizationRegistry>()
        ;
}
