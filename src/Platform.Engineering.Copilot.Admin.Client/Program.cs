using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.Toast;
using Blazored.Modal;
using Blazored.LocalStorage;
using Platform.Engineering.Copilot.Admin.Client;
using Platform.Engineering.Copilot.Admin.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load configuration
var apiBaseUrl = builder.Configuration.GetValue<string>("AdminApi:BaseUrl") ?? "http://localhost:5050";

// Register HttpClientFactory with named client for Admin API
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register Blazored services
builder.Services.AddBlazoredToast();
builder.Services.AddBlazoredModal();
builder.Services.AddBlazoredLocalStorage();

// Register application services
builder.Services.AddScoped<TemplateApiService>();
builder.Services.AddScoped<EnvironmentApiService>();
builder.Services.AddScoped<ComplianceApiService>();
builder.Services.AddScoped<AppSettingsService>();

var host = builder.Build();

// Initialize settings from localStorage
var settingsService = host.Services.GetRequiredService<AppSettingsService>();
await settingsService.InitializeAsync();

await host.RunAsync();
