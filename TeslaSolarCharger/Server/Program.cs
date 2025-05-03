using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PkSoftwareService.Custom.Backend;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
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
using TeslaSolarCharger.Server.Services;
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

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CarBasicConfigurationValidator>();

builder.Host.UseSerilog();
const string outputTemplate = "[{Timestamp:dd-MMM-yyyy HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";
var inMemorySink = new InMemorySink(outputTemplate, capacity: configurationManager.GetValue<int>("InMemoryLogDefaultCapacity"));

builder.Services.AddSingleton<IInMemorySink>(inMemorySink);
builder.Services.AddSingleton(inMemorySink);

var inMemoryLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
builder.Services.AddSingleton(inMemoryLevelSwitch);


var app = builder.Build();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()// overall minimum
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("TeslaSolarCharger.Shared.Wrappers.ConfigurationWrapper", LogEventLevel.Information)
    .MinimumLevel.Override("TeslaSolarCharger.Model.EntityFramework.DbConnectionStringHelper", LogEventLevel.Information)
    .WriteTo.Console(outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Debug)
    // Send events to the in–memory sink using a sub–logger and the dynamic level switch.
    .WriteTo.Logger(lc => lc
        .MinimumLevel.ControlledBy(inMemoryLevelSwitch)
        .WriteTo.Sink(inMemorySink))
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

app.UseWebSockets();
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

        var errorHandlingService = webApplication.Services.GetRequiredService<IErrorHandlingService>();
        await errorHandlingService.RemoveInvalidLoggedErrorsAsync().ConfigureAwait(false);


        var teslaFleetApiService = webApplication.Services.GetRequiredService<ITeslaFleetApiService>();
        await teslaFleetApiService.RefreshFleetApiRequestsAreAllowed();

        var shouldRetry = false;
        var baseConfiguration = await configurationWrapper.GetBaseConfigurationAsync();
        var baseConfigurationService = webApplication.Services.GetRequiredService<IBaseConfigurationService>();
        var teslaMateContextWrapper = webApplication.Services.GetRequiredService<ITeslaMateDbContextWrapper>();
        var teslaMateContext = teslaMateContextWrapper.GetTeslaMateContextIfAvailable();
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

        var tscConfigurationService = webApplication.Services.GetRequiredService<ITscConfigurationService>();
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var backendApiService = webApplication.Services.GetRequiredService<IBackendApiService>();
        var version = await backendApiService.GetCurrentVersion().ConfigureAwait(false);
        if (version != default && version.Contains('-'))
        {
            settings.IsPreRelease = true;
        }
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
        await configJsonService.SetCorrectHomeDetectionVia().ConfigureAwait(false);
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
                await carConfigurationService.AddAllMissingCarsFromTeslaAccount().ConfigureAwait(false);
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

        var meterValueEstimationService = webApplication.Services.GetRequiredService<IMeterValueEstimationService>();
        await meterValueEstimationService.FillMissingEstimatedMeterValuesInDatabase().ConfigureAwait(false);

        var jobManager = webApplication.Services.GetRequiredService<JobManager>();
        //if (!Debugger.IsAttached)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }

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
