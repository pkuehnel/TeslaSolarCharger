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
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
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
    .AddTransient<CarDbUpdateJob>()
    .AddTransient<JobFactory>()
    .AddTransient<IJobFactory, JobFactory>()
    .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
    .AddTransient<IChargingService, ChargingService>()
    .AddTransient<IGridService, GridService>()
    .AddTransient<IConfigService, ConfigService>()
    .AddTransient<IConfigJsonService, ConfigJsonService>()
    .AddTransient<IDateTimeProvider, DateTimeProvider>()
    .AddTransient<IChargeTimeUpdateService, ChargeTimeUpdateService>()
    .AddTransient<ITelegramService, TelegramService>()
    .AddTransient<ITeslaService, TeslamateApiService>()
    .AddSingleton<ISettings, Settings>()
    .AddSingleton<IInMemoryValues, InMemoryValues>()
    .AddSingleton<IConfigurationWrapper, ConfigurationWrapper>()
    .AddSingleton<IMqttNetLogger, MqttNetNullLogger>()
    .AddSingleton<IMqttClientAdapterFactory, MqttClientAdapterFactory>()
    .AddSingleton<IMqttClient, MqttClient>()
    .AddTransient<MqttFactory>()
    .AddTransient<IMqttService, MqttService>()
    .AddTransient<IPvValueService, PvValueService>()
    .AddTransient<IDbConnectionStringHelper, DbConnectionStringHelper>()
    .AddDbContext<ITeslamateContext, TeslamateContext>((provider, options) =>
    {
        options.UseNpgsql(provider.GetRequiredService<IDbConnectionStringHelper>().GetConnectionString());
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }, ServiceLifetime.Transient, ServiceLifetime.Transient)
    .AddTransient<ICarDbUpdateService, CarDbUpdateService>()
    .AddTransient<IEnvironmentVariableConverter, EnvironmentVariableConverter>()
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

var environmentVariableConverter = app.Services.GetRequiredService<IEnvironmentVariableConverter>();
await environmentVariableConverter.ConvertAllValues();

var telegramService = app.Services.GetRequiredService<ITelegramService>();
await telegramService.SendMessage("Application starting up");

var configJsonService = app.Services.GetRequiredService<IConfigJsonService>();

await configJsonService.AddCarIdsToSettings().ConfigureAwait(false);

var mqttHelper = app.Services.GetRequiredService<IMqttService>();

await mqttHelper.ConfigureMqttClient().ConfigureAwait(false);

var jobManager = app.Services.GetRequiredService<JobManager>();
jobManager.StartJobs();

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




