using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using SmartTeslaAmpSetter.Dtos;
using SmartTeslaAmpSetter.Scheduling;
using SmartTeslaAmpSetter.Services;
using SmartTeslaAmpSetter.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services
    .AddTransient<JobManager>()
    .AddTransient<Job>()
    .AddTransient<ConfigJsonUpdateJob>()
    .AddTransient<JobFactory>()
    .AddTransient<IJobFactory, JobFactory>()
    .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
    .AddTransient<ChargingService>()
    .AddTransient<ConfigJsonUpdateService>()
    .AddTransient<GridService>()
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
var carIds = app.Configuration.GetValue<string>("CarPriorities").Split("|");

var settings = app.Services.GetRequiredService<Settings>();

AddCarIdsToSettings(settings);

var jobManager = app.Services.GetRequiredService<JobManager>();
jobManager.StartJobs(jobIntervall);


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();


void AddCarIdsToSettings(Settings settings1)
{
    var configFileLocation = app.Configuration.GetValue<string>("ConfigFileLocation");
    if (File.Exists(configFileLocation))
    {
        var fileContent = File.ReadAllText(configFileLocation);
        settings1.Cars = JsonConvert.DeserializeObject<List<Car>>(fileContent) ?? throw new InvalidOperationException();
    }

    foreach (var car in settings1.Cars)
    {
        car.State.ShouldStopChargingSince = DateTime.MaxValue;
        car.State.ShouldStartChargingSince = DateTime.MaxValue;
    }
    var newCarIds = carIds.Where(i => !settings1.Cars.Any(c => c.Id.ToString().Equals(i))).ToList();
    foreach (var carId in newCarIds)
    {
        var id = int.Parse(carId);
        if (settings1.Cars.All(c => c.Id != id))
        {
            var car = new Car
            {
                Id = id,
                ChargeMode = ChargeMode.MaxPower,
                State =
                    {
                        ShouldStartChargingSince = DateTime.MaxValue,
                        ShouldStopChargingSince = DateTime.MaxValue
                    }
            };
            settings1.Cars.Add(car);
        }
    }

    if (settings1.Cars.Any(c => c.UpdatedSincLastWrite))
    {
        var json = JsonConvert.SerializeObject(settings1.Cars);
        var path = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName;
        path = Path.Combine(path, configFileLocation);
        var fileInfo = new FileInfo(path);
        if (!Directory.Exists(fileInfo.Directory.FullName))
        {
            Directory.CreateDirectory(fileInfo.Directory.FullName);
        }
        File.WriteAllText(path, json);
    }
}