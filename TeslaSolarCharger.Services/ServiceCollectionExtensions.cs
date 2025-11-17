using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Services.Services;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Modbus;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Mqtt;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.Template.Infrastructure;
using TeslaSolarCharger.Services.Services.Template.Infrastructure.Contracts;
using TeslaSolarCharger.Services.Services.Template.ValueSetupServices.Sma;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServicesDependencies(this IServiceCollection services) =>
            services
                .AddTransient<IRestValueConfigurationService, RestValueConfigurationService>()
                .AddTransient<IRestValueExecutionService, RestValueExecutionService>()
                .AddTransient<IValueOverviewService, ValueOverviewService>()
                .AddSingleton<IModbusClientHandlingService, ModbusClientHandlingService>()
                .AddTransient<IModbusTcpClient, CustomModbusTcpClient>()
                .AddTransient<IModbusValueConfigurationService, ModbusValueConfigurationService>()
                .AddTransient<IModbusValueExecutionService, ModbusValueExecutionService>()
                .AddTransient<IResultValueCalculationService, ResultValueCalculationService>()
                .AddTransient<IMqttConfigurationService, MqttConfigurationService>()
                .AddTransient<IGenericValueService, GenericValueService>()
                .AddSingleton<RefreshableValueHandlingService>()
                .AddSingleton<AutoRefreshingValueHandlingService>()

                .AddTransient<ITemplateValueConfigurationService, TemplateValueConfigurationService>()
                .AddTransient<ITemplateValueConfigurationFactory, TemplateValueConfigurationFactory>()
                .AddSingleton<IRefreshableValueHandlingService>(sp => sp.GetRequiredService<RefreshableValueHandlingService>())

                .AddTransient<IRefreshableValueSetupService, RestValueConfigurationService>()
                .AddTransient<IRefreshableValueSetupService, ModbusValueConfigurationService>()
                .AddTransient<IRefreshableValueSetupService, SmaInverterSetupService>()
                .AddTransient<IRefreshableValueSetupService, SmaHybridInverterSetupService>()

                .AddTransient<IAutoRefreshingValueSetupService, MqttClientSetupService>()
                .AddTransient<IAutoRefreshingValueSetupService, SmaEnergyMeterSetupService>()

                .AddTransient<IDecimalValueHandlingService>(sp => sp.GetRequiredService<AutoRefreshingValueHandlingService>())
                .AddTransient<IDecimalValueHandlingService>(sp => sp.GetRequiredService<RefreshableValueHandlingService>())

            ;
}
