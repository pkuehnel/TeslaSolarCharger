using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Implementations;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.MappingExtensions;
using TeslaSolarCharger.Server.Resources;
using TeslaSolarCharger.Server.Resources.PossibleIssues;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.TimeProviding;
using TeslaSolarCharger.Shared.Wrappers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddTransient<JobManager>()
    .AddTransient<ChargingValueJob>()
    .AddTransient<ConfigJsonUpdateJob>()
    .AddTransient<ChargeTimeUpdateJob>()
    .AddTransient<PvValueJob>()
    .AddTransient<PowerDistributionAddJob>()
    .AddTransient<HandledChargeFinalizingJob>()
    .AddTransient<MqttReconnectionJob>()
    .AddTransient<NewVersionCheckJob>()
    .AddTransient<JobFactory>()
    .AddTransient<IJobFactory, JobFactory>()
    .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
    .AddTransient<IChargingService, ChargingService>()
    .AddTransient<IConfigService, ConfigService>()
    .AddTransient<IConfigJsonService, ConfigJsonService>()
    .AddTransient<IDateTimeProvider, DateTimeProvider>()
    .AddTransient<IChargeTimeUpdateService, ChargeTimeUpdateService>()
    .AddTransient<ITelegramService, TelegramService>()
    .AddTransient<ITeslaService, TeslamateApiService>()
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
    .AddSingleton<GlobalConstants>()
    ;

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables();

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

if (environment == "Development")
{
    builder.Configuration.AddJsonFile("appsettings.Development.json");
}


var app = builder.Build();
//Do nothing before these lines as BaseConfig.json is created here. This results in breaking new installations!
var baseConfigurationConverter = app.Services.GetRequiredService<IBaseConfigurationConverter>();
await baseConfigurationConverter.ConvertAllEnvironmentVariables().ConfigureAwait(false);
await baseConfigurationConverter.ConvertBaseConfigToV1_0().ConfigureAwait(false);

var coreService = app.Services.GetRequiredService<ICoreService>();
coreService.LogVersion();

var teslaSolarChargerContext = app.Services.GetRequiredService<ITeslaSolarChargerContext>();
await teslaSolarChargerContext.Database.MigrateAsync().ConfigureAwait(false);

var chargingCostService = app.Services.GetRequiredService<IChargingCostService>();
await chargingCostService.DeleteDuplicatedHandleCharges().ConfigureAwait(false);

var configurationWrapper = app.Services.GetRequiredService<IConfigurationWrapper>();
await configurationWrapper.TryAutoFillUrls().ConfigureAwait(false);

var telegramService = app.Services.GetRequiredService<ITelegramService>();
await telegramService.SendMessage("Application starting up").ConfigureAwait(false);

var configJsonService = app.Services.GetRequiredService<IConfigJsonService>();

await configJsonService.AddCarIdsToSettings().ConfigureAwait(false);

var carDbUpdateService = app.Services.GetRequiredService<ICarDbUpdateService>();
await carDbUpdateService.UpdateMissingCarDataFromDatabase().ConfigureAwait(false);

var jobManager = app.Services.GetRequiredService<JobManager>();
await jobManager.StartJobs().ConfigureAwait(false);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
