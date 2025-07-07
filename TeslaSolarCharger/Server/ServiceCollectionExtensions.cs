using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Implementations;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Middlewares;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ApiServices;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
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
            .AddTransient<FleetTelemetryReconfigurationJob>()
            .AddTransient<WeatherDataRefreshJob>()
            .AddTransient<MeterValueDatabaseSaveJob>()
            .AddTransient<HomeBatteryMinSocRefreshJob>()
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
            .AddTransient<MqttClientFactory>()
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
                options.UseSqlite(provider.GetRequiredService<IDbConnectionStringHelper>().GetTeslaSolarChargerDbPath())
                    .AddInterceptors(new SqliteBusyTimeoutInterceptor(5000));
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }, ServiceLifetime.Transient, ServiceLifetime.Transient)
            .AddTransient<IPossibleIssues, PossibleIssues>()
            .AddTransient<IChargingCostService, ChargingCostService>()
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
            .AddTransient<IErrorDetectionService, ErrorDetectionService>()
            .AddTransient<ITeslaMateDbContextWrapper, TeslaMateDbContextWrapper>()
            .AddTransient<ITeslaService, TeslaFleetApiService>()
            .AddTransient<IPasswordGenerationService, PasswordGenerationService>()
            .AddTransient<IDebugService, DebugService>()
            .AddTransient<IFleetTelemetryConfigurationService, FleetTelemetryConfigurationService>()
            .AddTransient<IMeterValueLogService, MeterValueLogService>()
            .AddTransient<IWeatherDataService, WeatherDataService>()
            .AddTransient<IEnergyDataService, EnergyDataService>()
            .AddTransient<IMeterValueEstimationService, MeterValueEstimationService>()
            .AddTransient<IOcppChargePointActionService, OcppChargePointActionService>()
            .AddTransient<IOcppChargePointConfigurationService, OcppChargePointConfigurationService>()
            .AddTransient<IOcppChargingStationConfigurationService, OcppChargingStationConfigurationService>()
            .AddTransient<ILoadPointManagementService, LoadPointManagementService>()
            .AddTransient<IChargingServiceV2, ChargingServiceV2>()
            .AddTransient<IHomeService, HomeService>()
            .AddTransient<ISunCalculator, SunCalculator>()
            .AddTransient<IHomeBatteryEnergyCalculator, HomeBatteryEnergyCalculator>()
            .AddTransient<IShouldStartStopChargingCalculator, ShouldStartStopChargingCalculator>()
            .AddTransient<IValidFromToSplitter, ValidFromToSplitter>()
            .AddTransient<ITargetChargingValueCalculationService, TargetChargingValueCalculationService>()
            .AddTransient<IPowerToControlCalculationService, PowerToControlCalculationService>()
            .AddScoped<INotChargingWithExpectedPowerReasonHelper, NotChargingWithExpectedPowerReasonHelper>()
            //Needs to be Singleton due to WebSocketConnections and property updated dictionary
            .AddSingleton<IFleetTelemetryWebSocketService, FleetTelemetryWebSocketService>()
            .AddSingleton<ITimeSeriesDataService, TimeSeriesDataService>()
            .AddSingleton<IMeterValueBufferService, MeterValueBufferService>()
            .AddSingleton<IOcppWebSocketConnectionHandlingService, OcppWebSocketConnectionHandlingService>()
            .AddSingleton<IPvValueNotifier, PvValueNotifier>()
            .AddScoped<ErrorHandlingMiddleware>()
            .AddHostedService<MeterValueFlushService>()
            .AddSharedBackendDependencies();
        return services;
    }
}
