using PkSoftwareService.Custom.Backend;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TeslaSolarCharger.BleApi;
using TeslaSolarCharger.BleApi.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

const string outputTemplate = "[{Timestamp:dd-MMM-yyyy HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

// Keep logs in memory (downloadable via the support page) and only write Information and above to the
// console. This drastically reduces the amount of data Docker persists to disk for the container logs.
var inMemoryLogCapacity = builder.Configuration.GetValue<int?>("InMemoryLogDefaultCapacity") ?? 10000;
var inMemorySink = new InMemorySink(outputTemplate, capacity: inMemoryLogCapacity);
var inMemoryLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);

builder.Services.AddSingleton<IInMemorySink>(inMemorySink);
builder.Services.AddSingleton(inMemoryLevelSwitch);

builder.Host.UseSerilog();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Information)
    // Send events to the in-memory sink using a sub-logger and the dynamic level switch.
    .WriteTo.Logger(lc => lc
        .MinimumLevel.ControlledBy(inMemoryLevelSwitch)
        .WriteTo.Sink(inMemorySink))
    .CreateLogger();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddServiceDependencies();

var app = builder.Build();

var startupService = app.Services.GetRequiredService<IStartupService>();
await startupService.UpdateRequestsAllowed();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();
