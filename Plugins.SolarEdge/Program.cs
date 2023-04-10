using Plugins.SolarEdge;
using Plugins.SolarEdge.Contracts;
using Plugins.SolarEdge.Services;
using Serilog;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.TimeProviding;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SharedValues>();
builder.Services.AddTransient<ICurrentValuesService, CurrentValuesService>()
    .AddTransient<IDateTimeProvider, DateTimeProvider>()
    ;

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();
