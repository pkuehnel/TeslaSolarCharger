using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using SmartTeslaAmpSetter.Server.Scheduling;
using SmartTeslaAmpSetter.Server.Services;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddTransient<JobManager>()
    .AddTransient<Job>()
    .AddTransient<ConfigJsonUpdateJob>()
    .AddTransient<JobFactory>()
    .AddTransient<IJobFactory, JobFactory>()
    .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
    .AddTransient<ChargingService>()
    .AddTransient<GridService>()
    .AddTransient<ConfigService>()
    .AddTransient<ConfigJsonService>()
    .AddSingleton<Settings>()
    ;

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables();



var app = builder.Build();

var secondsFromConfig = app.Configuration.GetValue<double>("UpdateIntervalSeconds");
var jobIntervall = TimeSpan.FromSeconds(secondsFromConfig);

var settings = app.Services.GetRequiredService<Settings>();

await AddCarIdsToSettings(settings).ConfigureAwait(false);

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
    var configJsonService = app.Services.GetRequiredService<ConfigJsonService>();
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