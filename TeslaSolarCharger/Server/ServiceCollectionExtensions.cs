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
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ApiServices;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.TimeProviding;
using TeslaSolarCharger.Shared.Wrappers;
using TeslaSolarCharger.SharedBackend;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Server;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyDependencies(this IServiceCollection services)
    {
        services
            .AddSingleton<JobManager>()
            .AddTransient<ChargingValueJob>()
            .AddTransient<CarStateCachingJob>()
            .AddTransient<PvValueJob>()
            .AddTransient<ChargingDetailsAddJob>()
            .AddTransient<FinishedChargingProcessFinalizingJob>()
            .AddTransient<MqttReconnectionJob>()
            .AddTransient<NewVersionCheckJob>()
            .AddTransient<SpotPriceJob>()
            .AddTransient<BackendTokenRefreshJob>()
            .AddTransient<FleetApiTokenRefreshJob>()
            .AddTransient<VehicleDataRefreshJob>()
            .AddTransient<TeslaMateChargeCostUpdateJob>()
            .AddTransient<BackendNotificationRefreshJob>()
            .AddTransient<ErrorMessagingJob>()
            .AddTransient<ErrorDetectionJob>()
            .AddTransient<BleApiVersionDetectionJob>()
            .AddTransient<FleetTelemetryReconnectionJob>()
            .AddTransient<JobFactory>()
            .AddTransient<IJobFactory, JobFactory>()
            .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
            .AddTransient<IChargingService, ChargingService>()
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
            .AddTransient<IPossibleIssues, PossibleIssues>()
            .AddTransient<IChargingCostService, ChargingCostService>()
            .AddTransient<IMapperConfigurationFactory, MapperConfigurationFactory>()
            .AddTransient<ICoreService, CoreService>()
            .AddTransient<INewVersionCheckService, NewVersionCheckService>()
            .AddTransient<INodePatternTypeHelper, NodePatternTypeHelper>()
            .AddSingleton<IIssueKeys, IssueKeys>()
            .AddSingleton<ToolTipTextKeys>()
            .AddTransient<IIndexService, IndexService>()
            .AddTransient<ISpotPriceService, SpotPriceService>()
            .AddTransient<ILatestTimeToReachSocUpdateService, LatestTimeToReachSocUpdateService>()
            .AddTransient<IChargeTimeCalculationService, ChargeTimeCalculationService>()
            .AddTransient<ITeslaFleetApiService, TeslaFleetApiService>()
            .AddTransient<ITokenHelper, TokenHelper>()
            .AddTransient<ITscConfigurationService, TscConfigurationService>()
            .AddTransient<IBackendApiService, BackendApiService>()
            .AddTransient<ITscOnlyChargingCostService, TscOnlyChargingCostService>()
            .AddTransient<IFixedPriceService, FixedPriceService>()
            .AddTransient<IOldTscConfigPriceService, OldTscConfigPriceService>()
            .AddTransient<ITeslaMateChargeCostUpdateService, TeslaMateChargeCostUpdateService>()
            .AddTransient<IBleService, TeslaBleService>()
            .AddTransient<IBackendNotificationService, BackendNotificationService>()
            .AddTransient<ICarConfigurationService, CarConfigurationService>()
            .AddTransient<IErrorHandlingService, ErrorHandlingService>()
            .AddTransient<ITeslaMateDbContextWrapper, TeslaMateDbContextWrapper>()
            .AddTransient<ITeslaService, TeslaFleetApiService>()
            .AddTransient<IPasswordGenerationService, PasswordGenerationService>()
            .AddSingleton<IFleetTelemetryWebSocketService, FleetTelemetryWebSocketService>()
            .AddSingleton<ITimeSeriesDataService, TimeSeriesDataService>()
            .AddSharedBackendDependencies();
        return services;
    }
}
