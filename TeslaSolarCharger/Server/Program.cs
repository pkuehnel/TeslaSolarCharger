using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Interceptors;
using System.Reflection;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Middlewares;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.ServerValidators;
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
builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilterAttribute>())
    .AddNewtonsoftJson();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddMyDependencies();
builder.Services.AddSharedDependencies();
builder.Services.AddServicesDependencies();

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CarBasicConfigurationValidator>();


var app = builder.Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(app.Services.GetRequiredService<IConfiguration>())
    .Enrich.FromLogContext()
    .CreateLogger();


//Do nothing before these lines as BaseConfig.json is created here. This results in breaking new installations!
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogTrace("Logger created.");
var configurationWrapper = app.Services.GetRequiredService<IConfigurationWrapper>();
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

app.UseMiddleware<ErrorHandlingMiddleware>();
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

        var socLimit = await teslaSolarChargerContext.Cars.Where(c => c.Id == 3).Select(c => c.SocLimit).FirstOrDefaultAsync();
        logger.LogCritical("Soc Limit line 110: {socLimit}", socLimit);

        var teslaFleetApiService = webApplication.Services.GetRequiredService<ITeslaFleetApiService>();
        await teslaFleetApiService.RefreshFleetApiRequestsAreAllowed();
        logger.LogCritical("Soc Limit line 114: {socLimit}", socLimit);

        var shouldRetry = false;
        var baseConfiguration = await configurationWrapper.GetBaseConfigurationAsync();
        var baseConfigurationService = webApplication.Services.GetRequiredService<IBaseConfigurationService>();
        var teslaMateContextWrapper = webApplication.Services.GetRequiredService<ITeslaMateDbContextWrapper>();
        var teslaMateContext = teslaMateContextWrapper.GetTeslaMateContextIfAvailable();
        logger.LogCritical("Soc Limit line 121: {socLimit}", socLimit);

        if (teslaMateContext != default)
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
                    baseConfiguration.UseTeslaMateAsDataSource = false;
                    baseConfiguration.UseTeslaMateIntegration = false;
                    await baseConfigurationService.UpdateBaseConfigurationAsync(baseConfiguration);
                }
            }
        }
        logger.LogCritical("Soc Limit line 151: {socLimit}", socLimit);



        var tscConfigurationService = webApplication.Services.GetRequiredService<ITscConfigurationService>();
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var backendApiService = webApplication.Services.GetRequiredService<IBackendApiService>();
        var version = await backendApiService.GetCurrentVersion().ConfigureAwait(false);
        LogContext.PushProperty("InstallationId", installationId);
        LogContext.PushProperty("Version", version);

        await backendApiService.PostInstallationInformation("Startup").ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 163: {socLimit}", socLimit);

        var coreService = webApplication.Services.GetRequiredService<ICoreService>();
        await coreService.BackupDatabaseIfNeeded().ConfigureAwait(false);

        var life = webApplication.Services.GetRequiredService<IHostApplicationLifetime>();
        life.ApplicationStopped.Register(() =>
        {
            coreService.KillAllServices().GetAwaiter().GetResult();
        });

        var chargingCostService = webApplication.Services.GetRequiredService<IChargingCostService>();
        await chargingCostService.DeleteDuplicatedHandleCharges().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 176: {socLimit}", socLimit);



        await configurationWrapper1.TryAutoFillUrls().ConfigureAwait(false);

        var telegramService = webApplication.Services.GetRequiredService<ITelegramService>();
        await telegramService.SendMessage("Error messages via Telegram enabled. Note: Error and error resolved messages are only sent every five minutes.").ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 184: {socLimit}", socLimit);

        var configJsonService = webApplication.Services.GetRequiredService<IConfigJsonService>();
        await configJsonService.ConvertOldCarsToNewCar().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 188: {socLimit}", socLimit);

        await configJsonService.AddBleBaseUrlToAllCars().ConfigureAwait(false);
        //This needs to be done after converting old cars to new cars as IDs might change
        await chargingCostService.ConvertToNewChargingProcessStructure().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 193: {socLimit}", socLimit);
        await chargingCostService.FixConvertedChargingDetailSolarPower().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 195: {socLimit}", socLimit);
        await chargingCostService.AddFirstChargePrice().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 197: {socLimit}", socLimit);
        await chargingCostService.UpdateChargingProcessesAfterChargingDetailsFix().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 199: {socLimit}", socLimit);

        var carConfigurationService = webApplication.Services.GetRequiredService<ICarConfigurationService>();
        if (!configurationWrapper.ShouldUseFakeSolarValues())
        {
            await configJsonService.UpdateAverageGridVoltage().ConfigureAwait(false);
            try
            {
                await carConfigurationService.AddAllMissingCarsFromTeslaAccount().ConfigureAwait(false);
                logger.LogCritical("Soc Limit line 208: {socLimit}", socLimit);

            }
            catch
            {
                // Ignore this error as this could result in never taking the first token
            }
        }
        logger.LogCritical("Soc Limit line 216: {socLimit}", socLimit);
        await configJsonService.AddCarsToSettings().ConfigureAwait(false);
        logger.LogCritical("Soc Limit line 218: {socLimit}", socLimit);
        logger.LogCritical("Soc Limit dto {socLimit}", settings.Cars.Where(c => c.Id == 3).Select(c => c.SocLimit).FirstOrDefault());


        var pvValueService = webApplication.Services.GetRequiredService<IPvValueService>();
        await pvValueService.ConvertToNewConfiguration().ConfigureAwait(false);

        var spotPriceService = webApplication.Services.GetRequiredService<ISpotPriceService>();
        await spotPriceService.GetSpotPricesSinceFirstChargeDetail().ConfigureAwait(false);

        var homeGeofenceName = configurationWrapper.GeoFence();
        
        if (teslaMateContext != default && !string.IsNullOrEmpty(homeGeofenceName) && baseConfiguration is { HomeGeofenceLatitude: 52.5185238, HomeGeofenceLongitude: 13.3761736 })
        {
            logger.LogInformation("Convert home geofence from TeslaMate.");
            var homeGeofence = await teslaMateContext.Geofences.Where(g => g.Name == homeGeofenceName).FirstOrDefaultAsync();
            if (homeGeofence != null)
            {
                baseConfiguration.HomeGeofenceLatitude = Convert.ToDouble(homeGeofence.Latitude);
                baseConfiguration.HomeGeofenceLongitude = Convert.ToDouble(homeGeofence.Longitude);
                baseConfiguration.HomeGeofenceRadius = homeGeofence.Radius;
            }
        }
        await baseConfigurationService.UpdateBaseConfigurationAsync(baseConfiguration);
        var jobManager = webApplication.Services.GetRequiredService<JobManager>();
        //if (!Debugger.IsAttached)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }
        var errorHandlingService = webApplication.Services.GetRequiredService<IErrorHandlingService>();
        var issueKeys = webApplication.Services.GetRequiredService<IIssueKeys>();
        await errorHandlingService.HandleErrorResolved(issueKeys.CrashedOnStartup, null)
            .ConfigureAwait(false);
        await errorHandlingService.RemoveInvalidLoggedErrorsAsync().ConfigureAwait(false);
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
