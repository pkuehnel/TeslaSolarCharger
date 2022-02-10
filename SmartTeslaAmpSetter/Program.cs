using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    .AddTransient<JobFactory>()
    .AddTransient<IJobFactory, JobFactory>()
    .AddTransient<ISchedulerFactory, StdSchedulerFactory>()
    .AddTransient<ChargingService>()
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
    foreach (var carId in carIds)
    {
        var id = int.Parse(carId);
        if (settings1.Cars.All(c => c.Id != id))
        {
            settings1.Cars.Add(new Car()
            {
                Id = id,
                ShouldStartChargingSince = DateTime.MaxValue,
                ShouldStopChargingSince = DateTime.MaxValue,
                ChargeMode = ChargeMode.MaxPower,
            });
        }
    }
}