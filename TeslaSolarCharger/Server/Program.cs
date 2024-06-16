using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;
using System.Diagnostics;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Services;
using TeslaSolarCharger.Services.Services.Contracts;
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
var baseConfigurationConverter = app.Services.GetRequiredService<IBaseConfigurationConverter>();
await baseConfigurationConverter.ConvertAllEnvironmentVariables().ConfigureAwait(false);
await baseConfigurationConverter.ConvertBaseConfigToV1_0().ConfigureAwait(false);
DoStartupStuff(app, logger, configurationWrapper);

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

async Task DoStartupStuff(WebApplication webApplication, ILogger<Program> logger1, IConfigurationWrapper configurationWrapper1)
{
    try
    {
        //Do nothing before these lines as database is created here.
        var teslaSolarChargerContext = webApplication.Services.GetRequiredService<ITeslaSolarChargerContext>();
        await teslaSolarChargerContext.Database.MigrateAsync().ConfigureAwait(false);

        var shouldRetry = false;
        var teslaMateContext = webApplication.Services.GetRequiredService<ITeslamateContext>();
        try
        {
            var geofences = await teslaMateContext.Geofences.ToListAsync();
        }
        catch (Exception ex)
        {
            shouldRetry = true;
            logger1.LogError(ex, "TeslaMate Database not ready yet. Waiting for 20 seconds.");
            await Task.Delay(20000);
        }

        if (shouldRetry)
        {
            try
            {
                var geofences = await teslaMateContext.Geofences.ToListAsync();
            }
            catch (Exception ex)
            {
                logger1.LogError(ex, "TeslaMate Database still not ready. Throwing exception.");
                throw new Exception("TeslaMate database is not available. Check the database and restart TSC.");
            }
        }


        var tscConfigurationService = webApplication.Services.GetRequiredService<ITscConfigurationService>();
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var backendApiService = webApplication.Services.GetRequiredService<IBackendApiService>();
        var version = await backendApiService.GetCurrentVersion().ConfigureAwait(false);
        LogContext.PushProperty("InstallationId", installationId);
        LogContext.PushProperty("Version", version);

        await backendApiService.PostInstallationInformation("Startup").ConfigureAwait(false);

        var coreService = webApplication.Services.GetRequiredService<ICoreService>();
        await coreService.BackupDatabaseIfNeeded().ConfigureAwait(false);

        var life = webApplication.Services.GetRequiredService<IHostApplicationLifetime>();
        life.ApplicationStopped.Register(() =>
        {
            coreService.KillAllServices().GetAwaiter().GetResult();
        });

        var chargingCostService = webApplication.Services.GetRequiredService<IChargingCostService>();
        await chargingCostService.DeleteDuplicatedHandleCharges().ConfigureAwait(false);



        await configurationWrapper1.TryAutoFillUrls().ConfigureAwait(false);

        var telegramService = webApplication.Services.GetRequiredService<ITelegramService>();
        await telegramService.SendMessage("Application starting up").ConfigureAwait(false);

        var configJsonService = webApplication.Services.GetRequiredService<IConfigJsonService>();
        await configJsonService.ConvertOldCarsToNewCar().ConfigureAwait(false);
        //This needs to be done after converting old cars to new cars as IDs might change
        await chargingCostService.ConvertToNewChargingProcessStructure().ConfigureAwait(false);
        await chargingCostService.AddFirstChargePrice().ConfigureAwait(false);
        await configJsonService.UpdateAverageGridVoltage().ConfigureAwait(false);

        var carConfigurationService = webApplication.Services.GetRequiredService<ICarConfigurationService>();
        await carConfigurationService.AddAllMissingTeslaMateCars().ConfigureAwait(false);
        await configJsonService.AddCarsToSettings().ConfigureAwait(false);


        var pvValueService = webApplication.Services.GetRequiredService<IPvValueService>();
        await pvValueService.ConvertToNewConfiguration().ConfigureAwait(false);

        var spotPriceService = webApplication.Services.GetRequiredService<ISpotPriceService>();
        await spotPriceService.GetSpotPricesSinceFirstChargeDetail().ConfigureAwait(false);

        var jobManager = webApplication.Services.GetRequiredService<JobManager>();
        //if (!Debugger.IsAttached)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }
    }
    catch (Exception ex)
    {
        logger1.LogCritical(ex, "Crashed on startup");
        var settings = webApplication.Services.GetRequiredService<ISettings>();
        settings.CrashedOnStartup = true;
        settings.StartupCrashMessage = ex.Message;
        var backendApiService = webApplication.Services.GetRequiredService<IBackendApiService>();
        await backendApiService.PostErrorInformation(nameof(Program), "Startup",
                $"Exception Message: {ex.Message} StackTrace: {ex.StackTrace}")
            .ConfigureAwait(false);
    }
    finally
    {
        var settings = webApplication.Services.GetRequiredService<ISettings>();
        settings.IsStartupCompleted = true;
    }
}
