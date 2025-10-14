using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Platform.Engineering.Copilot.Chat.App.Data;
using Platform.Engineering.Copilot.Chat.App.Hubs;
using Platform.Engineering.Copilot.Chat.App.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/chat-app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                     "Data Source=chat.db"));

// Add HttpClient for API integration
builder.Services.AddHttpClient();

// Add SignalR with minimal configuration
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:5001") // React dev server
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Register services
builder.Services.AddScoped<IChatService, ChatService>();

// Add SPA services
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "ClientApp/build";
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseSpaStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Configure SPA - but exclude API routes from SPA proxy
app.MapWhen(context => !context.Request.Path.StartsWithSegments("/api") && 
                      !context.Request.Path.StartsWithSegments("/chathub"), 
    subApp =>
    {
        subApp.UseSpa(spa =>
        {
            spa.Options.SourcePath = "ClientApp";

            if (app.Environment.IsDevelopment())
            {
                spa.UseProxyToSpaDevelopmentServer("http://localhost:3000");
            }
        });
    });

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await context.Database.EnsureCreatedAsync();
    Log.Information("✅ Database initialized successfully");
}

Log.Information("🚀 Enhanced Chat Application starting on {Environment}", app.Environment.EnvironmentName);

app.Run();

// Ensure to flush and stop internal timers/threads before application-exit
Log.CloseAndFlush();