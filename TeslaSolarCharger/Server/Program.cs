using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using System.Diagnostics;
using TeslaSolarCharger.GridPriceProvider;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Services;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

var builder = WebApplication.CreateBuilder(args);

//To get valus from configuration before dependency injection is set up
var configurationManager = builder.Configuration;
// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var useFleetApi = configurationManager.GetValue<bool>("UseFleetApi");
builder.Services.AddMyDependencies(useFleetApi);
builder.Services.AddSharedDependencies();
builder.Services.AddGridPriceProvider();
builder.Services.AddServicesDependencies();

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

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(app.Services.GetRequiredService<IConfiguration>())
    .Enrich.FromLogContext()
    .CreateLogger();


//Do nothing before these lines as BaseConfig.json is created here. This results in breaking new installations!
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogTrace("Logger created.");
var configurationWrapper = app.Services.GetRequiredService<IConfigurationWrapper>();

try
{
    var baseConfigurationConverter = app.Services.GetRequiredService<IBaseConfigurationConverter>();
    await baseConfigurationConverter.ConvertAllEnvironmentVariables().ConfigureAwait(false);
    await baseConfigurationConverter.ConvertBaseConfigToV1_0().ConfigureAwait(false);


    //Do nothing before these lines as database is created here.
    var teslaSolarChargerContext = app.Services.GetRequiredService<ITeslaSolarChargerContext>();
    await teslaSolarChargerContext.Database.MigrateAsync().ConfigureAwait(false);

    var tscConfigurationService = app.Services.GetRequiredService<ITscConfigurationService>();
    var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
    var backendApiService = app.Services.GetRequiredService<IBackendApiService>();
    var version = await backendApiService.GetCurrentVersion().ConfigureAwait(false);
    LogContext.PushProperty("InstallationId", installationId);
    LogContext.PushProperty("Version", version);

    await backendApiService.PostInstallationInformation("Startup").ConfigureAwait(false);

    var coreService = app.Services.GetRequiredService<ICoreService>();
    await coreService.BackupDatabaseIfNeeded().ConfigureAwait(false);

    var life = app.Services.GetRequiredService<IHostApplicationLifetime>();
    life.ApplicationStopped.Register(() =>
    {
        coreService.KillAllServices().GetAwaiter().GetResult();
    });

    var chargingCostService = app.Services.GetRequiredService<IChargingCostService>();
    await chargingCostService.DeleteDuplicatedHandleCharges().ConfigureAwait(false);

    
    await configurationWrapper.TryAutoFillUrls().ConfigureAwait(false);

    var telegramService = app.Services.GetRequiredService<ITelegramService>();
    await telegramService.SendMessage("Application starting up").ConfigureAwait(false);

    var configJsonService = app.Services.GetRequiredService<IConfigJsonService>();
    await configJsonService.ConvertOldCarsToNewCar().ConfigureAwait(false);
    await configJsonService.AddCarsToSettings().ConfigureAwait(false);

    await configJsonService.UpdateAverageGridVoltage().ConfigureAwait(false);

    var pvValueService = app.Services.GetRequiredService<IPvValueService>();
    await pvValueService.ConvertToNewConfiguration().ConfigureAwait(false);

    var teslaFleetApiService = app.Services.GetRequiredService<ITeslaFleetApiService>();
    var settings = app.Services.GetRequiredService<ISettings>();
    if (await teslaFleetApiService.IsFleetApiProxyNeededInDatabase().ConfigureAwait(false))
    {
        settings.FleetApiProxyNeeded = true;
    }

    var jobManager = app.Services.GetRequiredService<JobManager>();
    if (!Debugger.IsAttached)
    {
        await jobManager.StartJobs().ConfigureAwait(false);
    }
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Crashed on startup");
    var settings = app.Services.GetRequiredService<ISettings>();
    settings.CrashedOnStartup = true;
    settings.StartupCrashMessage = ex.Message;
    var backendApiService = app.Services.GetRequiredService<IBackendApiService>();
    await backendApiService.PostErrorInformation(nameof(Program), "Startup",
            $"Exception Message: {ex.Message} StackTrace: {ex.StackTrace}")
        .ConfigureAwait(false);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

if (configurationWrapper.AllowCors())
{
    app.UseCors(x => x
        .AllowAnyMethod()
        .AllowAnyHeader()
        .SetIsOriginAllowed(_ => true) // allow any origin
        .AllowCredentials()); // allow credentials
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
