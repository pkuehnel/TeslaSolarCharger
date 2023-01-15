using Blazored.Toast;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TeslaSolarCharger.Client;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Helper;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.TimeProviding;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddBlazoredToast();
builder.Services.AddScoped<INodePatternTypeHelper, NodePatternTypeHelper>();
builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddSingleton<ToolTipTextKeys>();
await builder.Build().RunAsync().ConfigureAwait(false);
