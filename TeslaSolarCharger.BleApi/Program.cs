using Serilog;
using TeslaSolarCharger.BleApi;
using TeslaSolarCharger.BleApi.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);


builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));
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
