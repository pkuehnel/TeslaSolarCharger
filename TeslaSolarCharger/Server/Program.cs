using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;
using System.Diagnostics;
using TeslaSolarCharger.Client.Pages;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
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
    var settings = webApplication.Services.GetRequiredService<ISettings>();
    try
    {
        //Do nothing before these lines as database is created here.
        var teslaSolarChargerContext = webApplication.Services.GetRequiredService<ITeslaSolarChargerContext>();
        await teslaSolarChargerContext.Database.MigrateAsync().ConfigureAwait(false);

        var teslaFleetApiService = webApplication.Services.GetRequiredService<ITeslaFleetApiService>();
        await teslaFleetApiService.RefreshFleetApiRequestsAreAllowed();
        await teslaFleetApiService.RefreshTokensIfAllowedAndNeeded();

        var shouldRetry = false;
        var teslaMateContext = webApplication.Services.GetRequiredService<ITeslamateContext>();
        var baseConfiguration = await configurationWrapper.GetBaseConfigurationAsync();
        var baseConfigurationService = webApplication.Services.GetRequiredService<IBaseConfigurationService>();
        if (!configurationWrapper1.ShouldUseFakeSolarValues())
        {
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
                    logger1.LogError(ex, "TeslaMate is not available. Use TSC without TeslaMate integration");
                    settings.UseTeslaMate = false;
                    baseConfiguration.UseTeslaMateAsDataSource = false;
                    await baseConfigurationService.UpdateBaseConfigurationAsync(baseConfiguration);
                }
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
        await telegramService.SendMessage("Error messages via Telegram enabled. Note: Error and error resolved messages are only sent every five minutes.").ConfigureAwait(false);

        var configJsonService = webApplication.Services.GetRequiredService<IConfigJsonService>();
        await configJsonService.ConvertOldCarsToNewCar().ConfigureAwait(false);
        await configJsonService.AddBleBaseUrlToAllCars().ConfigureAwait(false);
        //This needs to be done after converting old cars to new cars as IDs might change
        await chargingCostService.ConvertToNewChargingProcessStructure().ConfigureAwait(false);
        await chargingCostService.FixConvertedChargingDetailSolarPower().ConfigureAwait(false);
        await chargingCostService.AddFirstChargePrice().ConfigureAwait(false);
        await chargingCostService.UpdateChargingProcessesAfterChargingDetailsFix().ConfigureAwait(false);

        var carConfigurationService = webApplication.Services.GetRequiredService<ICarConfigurationService>();
        if (!configurationWrapper.ShouldUseFakeSolarValues())
        {
            await configJsonService.UpdateAverageGridVoltage().ConfigureAwait(false);
            try
            {
                await carConfigurationService.AddAllMissingTeslaMateCars().ConfigureAwait(false);
            }
            catch
            {
                // Ignore this error as this could result in never taking the first token
            }
        }
        await configJsonService.AddCarsToSettings().ConfigureAwait(false);


        var pvValueService = webApplication.Services.GetRequiredService<IPvValueService>();
        await pvValueService.ConvertToNewConfiguration().ConfigureAwait(false);

        var spotPriceService = webApplication.Services.GetRequiredService<ISpotPriceService>();
        await spotPriceService.GetSpotPricesSinceFirstChargeDetail().ConfigureAwait(false);

        var homeGeofenceName = configurationWrapper.GeoFence();
        
        if (settings.UseTeslaMate && !string.IsNullOrEmpty(homeGeofenceName) && baseConfiguration is { HomeGeofenceLatitude: 0, HomeGeofenceLongitude: 0 })
        {
            var homeGeofence = await teslaMateContext.Geofences.Where(g => g.Name == homeGeofenceName).FirstOrDefaultAsync();
            if (homeGeofence != null)
            {
                baseConfiguration.HomeGeofenceLatitude = Convert.ToDouble(homeGeofence.Latitude);
                baseConfiguration.HomeGeofenceLongitude = Convert.ToDouble(homeGeofence.Longitude);
                baseConfiguration.HomeGeofenceRadius = homeGeofence.Radius;
                await baseConfigurationService.UpdateBaseConfigurationAsync(baseConfiguration);
            }
        }

        var jobManager = webApplication.Services.GetRequiredService<JobManager>();
        //if (!Debugger.IsAttached)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }
        var errorHandlingService = webApplication.Services.GetRequiredService<IErrorHandlingService>();
        var issueKeys = webApplication.Services.GetRequiredService<IIssueKeys>();
        await errorHandlingService.HandleErrorResolved(issueKeys.CrashedOnStartup, null)
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger1.LogCritical(ex, "Crashed on startup");
        settings.CrashedOnStartup = true;
        settings.StartupCrashMessage = ex.Message;
        var errorHandlingService = webApplication.Services.GetRequiredService<IErrorHandlingService>();
        var issueKeys = webApplication.Services.GetRequiredService<IIssueKeys>();
        await errorHandlingService.HandleError(nameof(Program), "Startup", "TSC crashed on startup",
                $"Exception Message: {ex.Message}", issueKeys.CrashedOnStartup, null, ex.StackTrace)
            .ConfigureAwait(false);
    }
    finally
    {
        settings.IsStartupCompleted = true;
        var dateTimeProvider = webApplication.Services.GetRequiredService<IDateTimeProvider>();
        settings.StartupTime = dateTimeProvider.UtcNow();
    }
}
