using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Platform.Engineering.Copilot.Admin.Client;
using Platform.Engineering.Copilot.Admin.Client.Services;
using Blazored.Toast;
using Blazored.Modal;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure Admin API base address
var apiBaseUrl = builder.Configuration["AdminApi:BaseUrl"] 
    ?? "https://localhost:5051";

builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(apiBaseUrl) 
});

// Register API services
builder.Services.AddScoped<TemplateApiService>();
builder.Services.AddScoped<EnvironmentApiService>();
builder.Services.AddScoped<ComplianceApiService>();
builder.Services.AddScoped<AppSettingsService>();

// Add Blazored services
builder.Services.AddBlazoredToast();
builder.Services.AddBlazoredModal();
builder.Services.AddBlazoredLocalStorage();

var host = builder.Build();

// Initialize app settings (load saved settings and apply theme)
var settingsService = host.Services.GetRequiredService<AppSettingsService>();
await settingsService.InitializeAsync();

await host.RunAsync();
