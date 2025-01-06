using Lysando.LabStorageV2.UiHelper.Wrapper;
using Lysando.LabStorageV2.UiHelper.Wrapper.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using MudExtensions.Services;
using TeslaSolarCharger.Client;
using TeslaSolarCharger.Client.Helper;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Helper;
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
builder.Services.AddScoped<IBackendApiTokenCheckService, BackendApiTokenCheckService>();
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
await builder.Build().RunAsync().ConfigureAwait(false);
