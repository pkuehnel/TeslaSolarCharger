using Microsoft.Extensions.DependencyInjection;
using TeslaSolarCharger.Services.Services;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Modbus;
using TeslaSolarCharger.Services.Services.Modbus.Contracts;
using TeslaSolarCharger.Services.Services.Mqtt;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest;
using TeslaSolarCharger.Services.Services.Rest.Contracts;

namespace TeslaSolarCharger.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServicesDependencies(this IServiceCollection services) =>
            services
                .AddTransient<IRestValueConfigurationService, RestValueConfigurationService>()
                .AddTransient<IRestValueExecutionService, RestValueExecutionService>()
                .AddSingleton<IModbusClientHandlingService, ModbusClientHandlingService>()
                .AddTransient<IModbusTcpClient, CustomModbusTcpClient>()
                .AddTransient<IModbusValueConfigurationService, ModbusValueConfigurationService>()
                .AddTransient<IModbusValueExecutionService, ModbusValueExecutionService>()
                .AddTransient<IResultValueCalculationService, ResultValueCalculationService>()
                .AddTransient<IMqttConfigurationService, MqttConfigurationService>()
                .AddSingleton<IMqttClientHandlingService, MqttClientHandlingService>()
                .AddTransient<IMqttExecutionService, MqttExecutionService>()
                .AddTransient<IMqttClientReconnectionService, MqttClientReconnectionService>()
            ;
}
