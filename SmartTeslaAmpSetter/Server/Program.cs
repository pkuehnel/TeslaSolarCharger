using MQTTnet;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using SmartTeslaAmpSetter.Server;
using SmartTeslaAmpSetter.Server.Contracts;
using SmartTeslaAmpSetter.Server.Scheduling;
using SmartTeslaAmpSetter.Server.Services;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.TimeProviding;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var mqttFactory = new MqttFactory();
var mqttClient = mqttFactory.CreateMqttClient();


builder.Services
    .AddTransient<JobManager>()
    .AddTransient<Job>()
    .AddTransient<ConfigJsonUpdateJob>()
    .AddTransient<JobFactory>()
    .AddTransient<IJobFactory, JobFactory>()
    .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
    .AddTransient<IChargingService, ChargingService>()
    .AddTransient<IGridService, GridService>()
    .AddTransient<IConfigService, ConfigService>()
    .AddTransient<IConfigJsonService, ConfigJsonService>()
    .AddTransient<IDateTimeProvider, DateTimeProvider>()
    .AddSingleton<Settings>()
    .AddSingleton(mqttClient)
    .AddTransient<MqttFactory>()
    .AddTransient<MqttHelper>()
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

var secondsFromConfig = app.Configuration.GetValue<double>("UpdateIntervalSeconds");
var jobIntervall = TimeSpan.FromSeconds(secondsFromConfig);

var settings = app.Services.GetRequiredService<Settings>();

await AddCarIdsToSettings(settings).ConfigureAwait(false);

var mqttHelper = app.Services.GetRequiredService<MqttHelper>();

await mqttHelper.ConfigureMqttClient().ConfigureAwait(false);

var jobManager = app.Services.GetRequiredService<JobManager>();
jobManager.StartJobs(jobIntervall);

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


async Task AddCarIdsToSettings(Settings settings1)
{
    var configJsonService = app.Services.GetRequiredService<IConfigJsonService>();
    settings1.Cars = await configJsonService.GetCarsFromConfiguration();
    foreach (var car in settings1.Cars)
    {
        if (car.CarConfiguration.UsableEnergy < 1)
        {
            car.CarConfiguration.UsableEnergy = 75;
        }

        if (car.CarConfiguration.MaximumAmpere < 1)
        {
            car.CarConfiguration.MaximumAmpere = 16;
        }

        if (car.CarConfiguration.MinimumAmpere < 16)
        {
            car.CarConfiguration.MinimumAmpere = 1;
        }
    }
}

