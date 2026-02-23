using Platform.Engineering.Copilot.Chat.Hubs;
using Platform.Engineering.Copilot.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// Register all platform copilot services (agents, tools, orchestrator, Azure OpenAI)
builder.Services.AddPlatformCopilotServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Map SignalR ChatHub endpoint per signalr-hub.md
app.MapHub<ChatHub>("/chathub");

app.Run();
