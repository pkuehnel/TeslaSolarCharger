using FluentValidation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PkSoftwareService.Custom.Backend;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using System.Reflection;
using TeslaSolarCharger.Client.Contracts;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Components;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Middlewares;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.ServerValidators;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Hubs;
using TeslaSolarCharger.Services;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources;

var builder = WebApplication.CreateBuilder(args);

//To get valus from configuration before dependency injection is set up
var configurationManager = builder.Configuration;
// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilterAttribute>())
    .AddNewtonsoftJson();
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR();

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
builder.Services.AddMudServices();
builder.Services.AddScoped<IIsStartupCompleteChecker, IsStartupCompleteChecker>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CarBasicConfigurationValidator>();

var maxFileSize = (long)1024 * 1024 * 1024 * 50; // 50GB
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxFileSize;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = maxFileSize;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Host.UseSerilog();
const string outputTemplate = "[{Timestamp:dd-MMM-yyyy HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";
var inMemorySink = new InMemorySink(outputTemplate, capacity: configurationManager.GetValue<int>("InMemoryLogDefaultCapacity"));

builder.Services.AddSingleton<IInMemorySink>(inMemorySink);
builder.Services.AddSingleton(inMemorySink);

var inMemoryLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
builder.Services.AddKeyedSingleton(StaticConstants.InMemoryLogDependencyInjectionKey, inMemoryLevelSwitch);

var fileLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Fatal);
builder.Services.AddKeyedSingleton(StaticConstants.FileLogDependencyInjectionKey, fileLevelSwitch);


var app = builder.Build();

var configurationWrapper = app.Services.GetRequiredService<IConfigurationWrapper>();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()// overall minimum
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("TeslaSolarCharger.Shared.Wrappers.ConfigurationWrapper", LogEventLevel.Information)
    .MinimumLevel.Override("TeslaSolarCharger.Model.EntityFramework.DbConnectionStringHelper", LogEventLevel.Information)
    .WriteTo.Console(outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Information)
    // Send events to the in–memory sink using a sub–logger and the dynamic level switch.
    .WriteTo.Logger(lc => lc
        .MinimumLevel.ControlledBy(inMemoryLevelSwitch)
        .WriteTo.Sink(inMemorySink))
        .WriteTo.Logger(lc => lc
            .MinimumLevel.ControlledBy(fileLevelSwitch)
            .WriteTo.File(
                path: Path.Combine(configurationWrapper.LogFilesDirectory(), "teslasolarcharger-.log"),
                rollingInterval: RollingInterval.Hour,
                retainedFileTimeLimit: TimeSpan.FromDays(2),
                outputTemplate: outputTemplate))
    .CreateLogger();


//Do nothing before these lines as BaseConfig.json is created here. This results in breaking new installations!
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogTrace("Logger created.");
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

app.UseAntiforgery();

app.UseMiddleware<StartupCheckMiddleware>();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TeslaSolarCharger.Client._Imports).Assembly);

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api/ocpp"),
    branch => branch.Use(async (ctx, next) =>
    {
        var state = ctx.RequestServices.GetRequiredService<ISettings>();
        if (!state.IsStartupCompleted)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsync("OCPP endpoint not ready");
            return;
        }
        await next();
    })
);

app.UseWebSockets();
app.MapControllers();
app.MapHub<AppStateHub>("/appStateHub");

app.Run();

async Task DoStartupStuff(WebApplication webApplication, ILogger<Program> logger1, IConfigurationWrapper configurationWrapper1)
{
    using var startupScope = webApplication.Services.CreateScope();
    var settings = startupScope.ServiceProvider.GetRequiredService<ISettings>();
    try
    {
        logger1.LogInformation("Starting application startup tasks...");
        await Task.Delay(10000).ConfigureAwait(false); // Wait 10seconds to allow kestrel to start properly
        //Do nothing before these lines as database is restored or created here.
        var baseConfigurationService = startupScope.ServiceProvider.GetRequiredService<IBaseConfigurationService>();
        baseConfigurationService.ProcessPendingRestore();
        var teslaSolarChargerContext = startupScope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        // Before migration, temporarily enable detailed EF Core logging
        var migrationLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Debug) // More detailed EF logs
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information) // SQL commands
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", LogEventLevel.Debug) // Infrastructure logs
            .WriteTo.Sink(inMemorySink)
            .CreateLogger();

        // Temporarily replace the logger
        var originalLogger = Log.Logger;
        Log.Logger = migrationLogger;

        try
        {
            logger.LogInformation("Starting database migration with detailed logging...");
            await teslaSolarChargerContext.Database.MigrateAsync().ConfigureAwait(false);
            logger.LogInformation("Database migration completed");
        }
        finally
        {
            // Restore original logger
            Log.Logger = originalLogger;
            migrationLogger.Dispose();
        }
        var errorHandlingService = startupScope.ServiceProvider.GetRequiredService<IErrorHandlingService>();
        await errorHandlingService.RemoveInvalidLoggedErrorsAsync().ConfigureAwait(false);


        var teslaFleetApiService = startupScope.ServiceProvider.GetRequiredService<ITeslaFleetApiService>();
        await teslaFleetApiService.RefreshFleetApiRequestsAreAllowed();

        var shouldRetry = false;
        var baseConfiguration = await configurationWrapper.GetBaseConfigurationAsync();
        
        var teslaMateContextWrapper = startupScope.ServiceProvider.GetRequiredService<ITeslaMateDbContextWrapper>();
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

        var tscConfigurationService = startupScope.ServiceProvider.GetRequiredService<ITscConfigurationService>();
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var backendApiService = startupScope.ServiceProvider.GetRequiredService<IBackendApiService>();
        var version = await backendApiService.GetCurrentVersion().ConfigureAwait(false);
        if (version != default && version.Contains('-'))
        {
            settings.IsPreRelease = true;
        }
        LogContext.PushProperty("InstallationId", installationId);
        LogContext.PushProperty("Version", version);

        await backendApiService.PostInstallationInformation("Startup").ConfigureAwait(false);

        var coreService = startupScope.ServiceProvider.GetRequiredService<ICoreService>();
        await coreService.BackupDatabaseIfNeeded().ConfigureAwait(false);

        var life = startupScope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
        life.ApplicationStopped.Register(() =>
        {
            coreService.KillAllServices().GetAwaiter().GetResult();
        });

        var chargingCostService = startupScope.ServiceProvider.GetRequiredService<IChargingCostService>();
        await chargingCostService.DeleteDuplicatedHandleCharges().ConfigureAwait(false);



        await configurationWrapper1.TryAutoFillUrls().ConfigureAwait(false);

        var telegramService = startupScope.ServiceProvider.GetRequiredService<ITelegramService>();
        await telegramService.SendMessage("Error messages via Telegram enabled. Note: Error and error resolved messages are only sent every five minutes.").ConfigureAwait(false);

        var configJsonService = startupScope.ServiceProvider.GetRequiredService<IConfigJsonService>();
        await configJsonService.ConvertOldCarsToNewCar().ConfigureAwait(false);
        await configJsonService.SetCorrectHomeDetectionVia().ConfigureAwait(false);
        await configJsonService.AddBleBaseUrlToAllCars().ConfigureAwait(false);
        //This needs to be done after converting old cars to new cars as IDs might change
        await chargingCostService.ConvertToNewChargingProcessStructure().ConfigureAwait(false);
        await chargingCostService.FixConvertedChargingDetailSolarPower().ConfigureAwait(false);
        await chargingCostService.AddFirstChargePrice().ConfigureAwait(false);
        await chargingCostService.UpdateChargingProcessesAfterChargingDetailsFix().ConfigureAwait(false);

        var tscOnlyChargingCostService = startupScope.ServiceProvider.GetRequiredService<ITscOnlyChargingCostService>();
        await tscOnlyChargingCostService.AddNonZeroMeterValuesCarsAndChargingStationsToSettings().ConfigureAwait(false);


        var meterValueImportService = startupScope.ServiceProvider.GetRequiredService<IMeterValueImportService>();
        await meterValueImportService.ImportMeterValuesFromChargingDetailsAsync().ConfigureAwait(false);

        await backendApiService.RefreshBackendTokenIfNeeded().ConfigureAwait(false);
        var fleetApiService = startupScope.ServiceProvider.GetRequiredService<ITeslaFleetApiService>();
        await fleetApiService.RefreshFleetApiTokenIfNeeded().ConfigureAwait(false);

        var carConfigurationService = startupScope.ServiceProvider.GetRequiredService<ICarConfigurationService>();
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


        var pvValueService = startupScope.ServiceProvider.GetRequiredService<IPvValueService>();
        await pvValueService.ConvertToNewConfiguration().ConfigureAwait(false);

        var spotPriceService = startupScope.ServiceProvider.GetRequiredService<ISpotPriceService>();
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

        var meterValueEstimationService = startupScope.ServiceProvider.GetRequiredService<IMeterValueEstimationService>();
        await meterValueEstimationService.FillMissingEstimatedMeterValuesInDatabase().ConfigureAwait(false);

        var jobManager = startupScope.ServiceProvider.GetRequiredService<JobManager>();
        //if (!Debugger.IsAttached)
        {
            await jobManager.StartJobs().ConfigureAwait(false);
        }

        var issueKeys = startupScope.ServiceProvider.GetRequiredService<IIssueKeys>();
        await errorHandlingService.HandleErrorResolved(issueKeys.CrashedOnStartup, null)
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger1.LogCritical(ex, "Crashed on startup");
        settings.CrashedOnStartup = true;
        settings.StartupCrashMessage = ex.Message;
        var errorHandlingService = startupScope.ServiceProvider.GetRequiredService<IErrorHandlingService>();
        var issueKeys = startupScope.ServiceProvider.GetRequiredService<IIssueKeys>();
        await errorHandlingService.HandleError(nameof(Program), "Startup", "TSC crashed on startup",
                $"Exception Message: {ex.Message}", issueKeys.CrashedOnStartup, null, ex.StackTrace)
            .ConfigureAwait(false);
    }
    finally
    {
        settings.IsStartupCompleted = true;
        var dateTimeProvider = startupScope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        settings.StartupTime = dateTimeProvider.UtcNow();
    }
}
