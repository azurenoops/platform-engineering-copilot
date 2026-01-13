using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Platform.Engineering.Copilot.Admin.Client;
using Platform.Engineering.Copilot.Admin.Client.Services;
using Blazored.Toast;
using Blazored.Modal;

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

// Add Blazored services
builder.Services.AddBlazoredToast();
builder.Services.AddBlazoredModal();

await builder.Build().RunAsync();
