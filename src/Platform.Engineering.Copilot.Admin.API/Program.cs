using Serilog;
using Serilog.Events;
using Platform.Engineering.Copilot.Admin.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File("logs/admin-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Platform Engineering Copilot - Admin API",
        Version = "v1",
        Description = "REST API for managing Service Templates and Provisioned Environments"
    });
});

// Add CORS for Admin Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminClient", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5000",  // Admin Client default
                "http://localhost:5200",  // Admin Client dev
                "https://localhost:5201"  // Admin Client HTTPS
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Register Admin services
builder.Services.AddAdminServices(builder.Configuration);

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Admin API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AdminClient");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Log.Information("🚀 Platform Engineering Admin API starting on {Urls}", 
    string.Join(", ", app.Urls.DefaultIfEmpty("http://localhost:5050")));

app.Run();
