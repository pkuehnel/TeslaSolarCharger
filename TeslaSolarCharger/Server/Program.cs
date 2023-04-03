using Microsoft.EntityFrameworkCore;
using Serilog;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMyDependencies();


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
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogTrace("Logger created.");
var configurationWrapper = app.Services.GetRequiredService<IConfigurationWrapper>();

try
{


    var baseConfigurationConverter = app.Services.GetRequiredService<IBaseConfigurationConverter>();
    await baseConfigurationConverter.ConvertAllEnvironmentVariables().ConfigureAwait(false);
    await baseConfigurationConverter.ConvertBaseConfigToV1_0().ConfigureAwait(false);

    var coreService = app.Services.GetRequiredService<ICoreService>();
    coreService.LogVersion();

    await coreService.BackupDatabaseIfNeeded().ConfigureAwait(false);

    var life = app.Services.GetRequiredService<IHostApplicationLifetime>();
    life.ApplicationStopped.Register(() =>
    {
        coreService.KillAllServices().GetAwaiter().GetResult();
    });

    var teslaSolarChargerContext = app.Services.GetRequiredService<ITeslaSolarChargerContext>();
    await teslaSolarChargerContext.Database.MigrateAsync().ConfigureAwait(false);

    var chargingCostService = app.Services.GetRequiredService<IChargingCostService>();
    await chargingCostService.DeleteDuplicatedHandleCharges().ConfigureAwait(false);

    
    await configurationWrapper.TryAutoFillUrls().ConfigureAwait(false);

    var telegramService = app.Services.GetRequiredService<ITelegramService>();
    await telegramService.SendMessage("Application starting up").ConfigureAwait(false);

    var configJsonService = app.Services.GetRequiredService<IConfigJsonService>();
    await configJsonService.AddCarIdsToSettings().ConfigureAwait(false);

    await configJsonService.UpdateAverageGridVoltage().ConfigureAwait(false);

    var carDbUpdateService = app.Services.GetRequiredService<ICarDbUpdateService>();
    await carDbUpdateService.UpdateMissingCarDataFromDatabase().ConfigureAwait(false);

    var jobManager = app.Services.GetRequiredService<JobManager>();
    await jobManager.StartJobs().ConfigureAwait(false);
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Crached on startup");
    throw;
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
