using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Implementations;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.MappingExtensions;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ApiServices;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.TimeProviding;
using TeslaSolarCharger.Shared.Wrappers;
using TeslaSolarCharger.SharedBackend;

namespace TeslaSolarCharger.Server;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyDependencies(this IServiceCollection services, bool useFleetApi)
    {
        services
            .AddSingleton<JobManager>()
            .AddTransient<ChargingValueJob>()
            .AddTransient<CarStateCachingJob>()
            .AddTransient<PvValueJob>()
            .AddTransient<PowerDistributionAddJob>()
            .AddTransient<HandledChargeFinalizingJob>()
            .AddTransient<MqttReconnectionJob>()
            .AddTransient<NewVersionCheckJob>()
            .AddTransient<SpotPriceJob>()
            .AddTransient<JobFactory>()
            .AddTransient<IJobFactory, JobFactory>()
            .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
            .AddTransient<IChargingService, ChargingService>()
            .AddTransient<IConfigService, ConfigService>()
            .AddTransient<IConfigJsonService, ConfigJsonService>()
            .AddTransient<IDateTimeProvider, DateTimeProvider>()
            .AddTransient<ITelegramService, TelegramService>()
            .AddSingleton<ISettings, Settings>()
            .AddSingleton<IInMemoryValues, InMemoryValues>()
            .AddSingleton<IConfigurationWrapper, ConfigurationWrapper>()
            .AddTransient<IMqttNetLogger, MqttNetNullLogger>()
            .AddTransient<IMqttClientAdapterFactory, MqttClientAdapterFactory>()
            .AddTransient<IMqttClient, MqttClient>()
            .AddTransient<MqttFactory>()
            .AddSingleton<ITeslaMateMqttService, TeslaMateMqttService>()
            .AddSingleton<ISolarMqttService, SolarMqttService>()
            .AddSingleton<IMqttConnectionService, MqttConnectionService>()
            .AddTransient<IPvValueService, PvValueService>()
            .AddTransient<IBaseConfigurationService, BaseConfigurationService>()
            .AddTransient<IDbConnectionStringHelper, DbConnectionStringHelper>()
            .AddDbContext<ITeslamateContext, TeslamateContext>((provider, options) =>
            {
                options.UseNpgsql(provider.GetRequiredService<IDbConnectionStringHelper>().GetTeslaMateConnectionString());
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }, ServiceLifetime.Transient, ServiceLifetime.Transient)
            .AddDbContext<ITeslaSolarChargerContext, TeslaSolarChargerContext>((provider, options) =>
            {
                options.UseSqlite(provider.GetRequiredService<IDbConnectionStringHelper>().GetTeslaSolarChargerDbPath());
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }, ServiceLifetime.Transient, ServiceLifetime.Transient)
            .AddTransient<ICarDbUpdateService, CarDbUpdateService>()
            .AddTransient<IBaseConfigurationConverter, BaseConfigurationConverter>()
            .AddSingleton<IPossibleIssues, PossibleIssues>()
            .AddTransient<IIssueValidationService, IssueValidationService>()
            .AddTransient<IChargingCostService, ChargingCostService>()
            .AddTransient<IMapperConfigurationFactory, MapperConfigurationFactory>()
            .AddTransient<ICoreService, CoreService>()
            .AddTransient<INewVersionCheckService, NewVersionCheckService>()
            .AddTransient<INodePatternTypeHelper, NodePatternTypeHelper>()
            .AddSingleton<IssueKeys>()
            .AddSingleton<ToolTipTextKeys>()
            .AddTransient<IIndexService, IndexService>()
            .AddTransient<ISpotPriceService, SpotPriceService>()
            .AddTransient<ILatestTimeToReachSocUpdateService, LatestTimeToReachSocUpdateService>()
            .AddTransient<IChargeTimeCalculationService, ChargeTimeCalculationService>()
            .AddSharedBackendDependencies();
        if (useFleetApi)
        {
            services.AddTransient<ITeslaService, TeslaFleetApiService>();
        }
        else
        {
            services.AddTransient<ITeslaService, TeslamateApiService>();
        }

        return services;
    }
}
