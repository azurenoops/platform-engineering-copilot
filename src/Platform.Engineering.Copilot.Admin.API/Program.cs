using Microsoft.AspNetCore.Authentication.JwtBearer;
using Platform.Engineering.Copilot.Admin.API.Extensions;
using Serilog;

// ── Serilog bootstrap ──
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/platform-copilot-admin-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console()
        .WriteTo.File("logs/platform-copilot-admin-.log", rollingInterval: RollingInterval.Day));

    // Controllers
    builder.Services.AddControllers();

    // Admin services (EF Core, domain services, background services)
    builder.Services.AddAdminServices(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks();

    // Authentication — JwtBearer with Azure Government .us authority
    var devBypass = builder.Configuration.GetValue<bool>("Authentication:DevBypass");
    if (devBypass)
    {
        // Development bypass — no real token validation
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = false,
                    SignatureValidator = (token, _) => new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token)
                };
            });
    }
    else
    {
        var tenantId = builder.Configuration["Authentication:TenantId"];
        var audience = builder.Configuration["Authentication:Audience"] ?? "api://platform-copilot";
        var authority = builder.Configuration["Authentication:Authority"] ?? "https://login.microsoftonline.us";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{authority}/{tenantId}";
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
            });
    }

    // Authorization policies
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
        options.AddPolicy("Engineer", policy => policy.RequireRole("Admin", "Engineer"));
    });

    // CORS
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    // Swagger (Swashbuckle)
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = builder.Configuration["Swagger:Title"] ?? "Platform Copilot Admin API",
            Version = builder.Configuration["Swagger:Version"] ?? "v1"
        });

        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Configure port 5050 per admin-api.md
    builder.WebHost.UseUrls("http://localhost:5050");

    var app = builder.Build();

    // Middleware pipeline
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Admin API v1"));
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("Platform Copilot Admin API starting on port 5050");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Admin API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Partial class to enable WebApplicationFactory in integration tests.</summary>
public partial class Program { }
