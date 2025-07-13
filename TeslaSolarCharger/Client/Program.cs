using ApexCharts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using PkSoftwareService.Custom.Backend;
using Serilog;
using Serilog.Events;
using TeslaSolarCharger.Client;
using TeslaSolarCharger.Client.Helper;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Helper.Contracts;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.TimeProviding;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
//builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://192.168.1.50:7190/") });
builder.Services.AddScoped<INodePatternTypeHelper, NodePatternTypeHelper>();
builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddScoped<IDialogHelper, DialogHelper>();
builder.Services.AddScoped<IJavaScriptWrapper, JavaScriptWrapper>();
builder.Services.AddScoped<IHttpClientHelper, HttpClientHelper>();
builder.Services.AddScoped<ICloudConnectionCheckService, CloudConnectionCheckService>();
builder.Services.AddScoped<IEnergyDataService, EnergyDataService>();
builder.Services.AddScoped<IChargingStationsService, ChargingStationsService>();
builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IEntityKeyGenerationHelper, EntityKeyGenerationHelper>();
builder.Services.AddTransient<IChartWidthCalculator, ChartWidthCalculator>();
builder.Services.AddTransient<IApexChartHelper, ApexChartHelper>();
builder.Services.AddSingleton<ISignalRStateService, SignalRStateService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ToolTipTextKeys>();
builder.Services.AddSharedDependencies();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 250;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
})
    .AddMudExtensions();
const string outputTemplate = "[{Timestamp:dd-MMM-yyyy HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";
var inMemorySink = new InMemorySink(outputTemplate, capacity: 3000);
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()// overall minimum
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("MudBlazor.KeyInterceptorService", LogEventLevel.Debug)
    .MinimumLevel.Override("TeslaSolarCharger.Shared.Helper.StringHelper", LogEventLevel.Debug)
    .WriteTo.Logger(lc => lc
        .WriteTo.Sink(inMemorySink))
    .CreateLogger();

builder.Services.AddSingleton<IInMemorySink>(inMemorySink);

builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

builder.Services.AddApexCharts();
await builder.Build().RunAsync().ConfigureAwait(false);
