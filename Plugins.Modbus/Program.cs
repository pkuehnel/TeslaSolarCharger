using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Plugins.Modbus.Contracts;
using Plugins.Modbus.Services;
using Serilog;
using ModbusClient = Plugins.Modbus.Services.ModbusClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson(o =>
{
    o.SerializerSettings.Converters.Add(new StringEnumConverter()
    {
        NamingStrategy = new CamelCaseNamingStrategy(),
    });
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGenNewtonsoftSupport();

builder.Services.AddSingleton<IModbusService, ModbusService>();
builder.Services.AddTransient<IModbusClient, ModbusClient>();

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
