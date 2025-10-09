using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Application
{
    /// <summary>
    /// .NET application code generator
    /// </summary>
    public class DotNetCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            var app = request.Application!;

            // Generate Program.cs
            files["src/Program.cs"] = GenerateProgramCs(request);

            // Generate project file
            files["src/" + request.ServiceName + ".csproj"] = GenerateProjectFile(request);

            // Generate appsettings
            files["src/appsettings.json"] = GenerateAppSettings(request);
            files["src/appsettings.Development.json"] = GenerateDevAppSettings(request);

            // Generate Dockerfile
            files["Dockerfile"] = GenerateDockerfile(request);

            // Generate .dockerignore
            files[".dockerignore"] = GenerateDockerIgnore();

            // Generate health endpoint
            if (app.IncludeHealthCheck)
            {
                files["src/HealthCheck.cs"] = GenerateHealthCheck(request);
            }

            return Task.FromResult(files);
        }

        private string GenerateProgramCs(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            var app = request.Application!;

            sb.AppendLine("using Microsoft.AspNetCore.Builder;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.Hosting;");
            sb.AppendLine();
            sb.AppendLine($"var builder = WebApplication.CreateBuilder(args);");
            sb.AppendLine();
            sb.AppendLine("// Add services");
            sb.AppendLine("builder.Services.AddControllers();");
            sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
            sb.AppendLine("builder.Services.AddSwaggerGen();");
            
            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("builder.Services.AddApplicationInsightsTelemetry();");
            }

            if (app.IncludeHealthCheck)
            {
                sb.AppendLine("builder.Services.AddHealthChecks();");
            }

            sb.AppendLine();
            sb.AppendLine("var app = builder.Build();");
            sb.AppendLine();
            sb.AppendLine("// Configure pipeline");
            sb.AppendLine("if (app.Environment.IsDevelopment())");
            sb.AppendLine("{");
            sb.AppendLine("    app.UseSwagger();");
            sb.AppendLine("    app.UseSwaggerUI();");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("app.UseHttpsRedirection();");
            sb.AppendLine("app.UseAuthorization();");
            sb.AppendLine("app.MapControllers();");
            
            if (app.IncludeHealthCheck)
            {
                sb.AppendLine();
                sb.AppendLine("// Health endpoints");
                sb.AppendLine("app.MapHealthChecks(\"/health\");");
                sb.AppendLine("app.MapGet(\"/ready\", () => Results.Ok(new { status = \"ready\" }));");
            }

            sb.AppendLine();
            sb.AppendLine($"app.Run();");

            return sb.ToString();
        }

        private string GenerateProjectFile(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
            sb.AppendLine();
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <PackageReference Include=\"Microsoft.AspNetCore.OpenApi\" Version=\"8.0.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Swashbuckle.AspNetCore\" Version=\"6.5.0\" />");
            
            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("    <PackageReference Include=\"Microsoft.ApplicationInsights.AspNetCore\" Version=\"2.22.0\" />");
            }

            foreach (var db in request.Databases)
            {
                var package = db.Type switch
                {
                    DatabaseType.PostgreSQL => "Npgsql.EntityFrameworkCore.PostgreSQL",
                    DatabaseType.SQLServer => "Microsoft.EntityFrameworkCore.SqlServer",
                    DatabaseType.MySQL => "Pomelo.EntityFrameworkCore.MySql",
                    DatabaseType.MongoDB => "MongoDB.Driver",
                    DatabaseType.Redis => "StackExchange.Redis",
                    _ => null
                };
                
                if (package != null)
                {
                    sb.AppendLine($"    <PackageReference Include=\"{package}\" Version=\"8.0.0\" />");
                }
            }

            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
            sb.AppendLine("</Project>");

            return sb.ToString();
        }

        private string GenerateAppSettings(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"Logging\": {");
            sb.AppendLine("    \"LogLevel\": {");
            sb.AppendLine("      \"Default\": \"Information\",");
            sb.AppendLine("      \"Microsoft.AspNetCore\": \"Warning\"");
            sb.AppendLine("    }");
            sb.AppendLine("  },");
            
            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("  \"ApplicationInsights\": {");
                sb.AppendLine("    \"ConnectionString\": \"${APP_INSIGHTS_CONNECTION_STRING}\"");
                sb.AppendLine("  },");
            }

            sb.AppendLine("  \"AllowedHosts\": \"*\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateDevAppSettings(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"Logging\": {");
            sb.AppendLine("    \"LogLevel\": {");
            sb.AppendLine("      \"Default\": \"Debug\",");
            sb.AppendLine("      \"Microsoft.AspNetCore\": \"Information\"");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateDockerfile(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Build stage");
            sb.AppendLine("FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build");
            sb.AppendLine("WORKDIR /src");
            sb.AppendLine();
            sb.AppendLine($"COPY [\"src/{request.ServiceName}.csproj\", \"src/\"]");
            sb.AppendLine("RUN dotnet restore \"src/" + request.ServiceName + ".csproj\"");
            sb.AppendLine();
            sb.AppendLine("COPY . .");
            sb.AppendLine("WORKDIR \"/src/src\"");
            sb.AppendLine($"RUN dotnet build \"{request.ServiceName}.csproj\" -c Release -o /app/build");
            sb.AppendLine();
            sb.AppendLine("# Publish stage");
            sb.AppendLine("FROM build AS publish");
            sb.AppendLine($"RUN dotnet publish \"{request.ServiceName}.csproj\" -c Release -o /app/publish");
            sb.AppendLine();
            sb.AppendLine("# Runtime stage");
            sb.AppendLine("FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final");
            sb.AppendLine("WORKDIR /app");
            sb.AppendLine($"EXPOSE {request.Application?.Port ?? 8080}");
            sb.AppendLine();
            sb.AppendLine("COPY --from=publish /app/publish .");
            sb.AppendLine($"ENTRYPOINT [\"dotnet\", \"{request.ServiceName}.dll\"]");
            return sb.ToString();
        }

        private string GenerateHealthCheck(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.Extensions.Diagnostics.HealthChecks;");
            sb.AppendLine();
            sb.AppendLine("public class CustomHealthCheck : IHealthCheck");
            sb.AppendLine("{");
            sb.AppendLine("    public Task<HealthCheckResult> CheckHealthAsync(");
            sb.AppendLine("        HealthCheckContext context,");
            sb.AppendLine("        CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        // Add custom health check logic here");
            sb.AppendLine("        var isHealthy = true;");
            sb.AppendLine();
            sb.AppendLine("        if (isHealthy)");
            sb.AppendLine("        {");
            sb.AppendLine("            return Task.FromResult(HealthCheckResult.Healthy(\"Service is healthy\"));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return Task.FromResult(HealthCheckResult.Unhealthy(\"Service is unhealthy\"));");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateDockerIgnore()
        {
            return @"**/.dockerignore
**/.git
**/.gitignore
**/.vs
**/.vscode
**/bin
**/obj
**/*.user
**/*.suo
";
        }
    }

    /// <summary>
    /// Node.js application code generator
    /// </summary>
    public class NodeJSCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            files["src/index.js"] = GenerateIndexJs(request);
            files["package.json"] = GeneratePackageJson(request);
            files["Dockerfile"] = GenerateDockerfile(request);
            files[".dockerignore"] = GenerateDockerIgnore();
            files[".eslintrc.json"] = GenerateEslintConfig();

            return Task.FromResult(files);
        }

        private string GenerateIndexJs(TemplateGenerationRequest request)
        {
            var framework = request.Application?.Framework?.ToLower() ?? "express";
            var sb = new StringBuilder();

            sb.AppendLine("const express = require('express');");
            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("const appInsights = require('applicationinsights');");
            }
            sb.AppendLine();

            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("// Initialize Application Insights");
                sb.AppendLine("appInsights.setup(process.env.APPLICATIONINSIGHTS_CONNECTION_STRING).start();");
                sb.AppendLine();
            }

            sb.AppendLine("const app = express();");
            sb.AppendLine($"const PORT = process.env.PORT || {request.Application?.Port ?? 3000};");
            sb.AppendLine();
            sb.AppendLine("// Middleware");
            sb.AppendLine("app.use(express.json());");
            sb.AppendLine();
            sb.AppendLine("// Routes");
            sb.AppendLine("app.get('/', (req, res) => {");
            sb.AppendLine($"  res.json({{ message: 'Welcome to {request.ServiceName}' }});");
            sb.AppendLine("});");
            sb.AppendLine();

            if (request.Application?.IncludeHealthCheck == true)
            {
                sb.AppendLine("// Health endpoints");
                sb.AppendLine("app.get('/health', (req, res) => {");
                sb.AppendLine("  res.json({ status: 'healthy' });");
                sb.AppendLine("});");
                sb.AppendLine();
                sb.AppendLine("app.get('/ready', (req, res) => {");
                sb.AppendLine("  res.json({ status: 'ready' });");
                sb.AppendLine("});");
                sb.AppendLine();
            }

            sb.AppendLine("// Start server");
            sb.AppendLine("app.listen(PORT, () => {");
            sb.AppendLine("  console.log(`Server running on port ${PORT}`);");
            sb.AppendLine("});");

            return sb.ToString();
        }

        private string GeneratePackageJson(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"name\": \"{request.ServiceName}\",");
            sb.AppendLine("  \"version\": \"1.0.0\",");
            sb.AppendLine($"  \"description\": \"{request.Description}\",");
            sb.AppendLine("  \"main\": \"src/index.js\",");
            sb.AppendLine("  \"scripts\": {");
            sb.AppendLine("    \"start\": \"node src/index.js\",");
            sb.AppendLine("    \"dev\": \"nodemon src/index.js\",");
            sb.AppendLine("    \"test\": \"jest\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"dependencies\": {");
            sb.AppendLine("    \"express\": \"^4.18.2\"");

            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("    ,\"applicationinsights\": \"^2.9.0\"");
            }

            foreach (var db in request.Databases)
            {
                var package = db.Type switch
                {
                    DatabaseType.PostgreSQL => "pg",
                    DatabaseType.MySQL => "mysql2",
                    DatabaseType.MongoDB => "mongodb",
                    DatabaseType.Redis => "redis",
                    _ => null
                };
                
                if (package != null)
                {
                    sb.AppendLine($"    ,\"{package}\": \"latest\"");
                }
            }

            sb.AppendLine("  },");
            sb.AppendLine("  \"devDependencies\": {");
            sb.AppendLine("    \"nodemon\": \"^3.0.1\",");
            sb.AppendLine("    \"jest\": \"^29.7.0\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateDockerfile(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("FROM node:20-alpine AS build");
            sb.AppendLine("WORKDIR /app");
            sb.AppendLine();
            sb.AppendLine("COPY package*.json ./");
            sb.AppendLine("RUN npm ci --only=production");
            sb.AppendLine();
            sb.AppendLine("FROM node:20-alpine");
            sb.AppendLine("WORKDIR /app");
            sb.AppendLine();
            sb.AppendLine("COPY --from=build /app/node_modules ./node_modules");
            sb.AppendLine("COPY . .");
            sb.AppendLine();
            sb.AppendLine($"EXPOSE {request.Application?.Port ?? 3000}");
            sb.AppendLine("USER node");
            sb.AppendLine();
            sb.AppendLine("CMD [\"node\", \"src/index.js\"]");
            return sb.ToString();
        }

        private string GenerateDockerIgnore()
        {
            return @"node_modules
npm-debug.log
.git
.gitignore
.env
*.md
";
        }

        private string GenerateEslintConfig()
        {
            return @"{
  ""env"": {
    ""node"": true,
    ""es2021"": true
  },
  ""extends"": ""eslint:recommended"",
  ""parserOptions"": {
    ""ecmaVersion"": 12
  }
}";
        }
    }

    /// <summary>
    /// Python application code generator
    /// </summary>
    public class PythonCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            files["src/main.py"] = GenerateMainPy(request);
            files["requirements.txt"] = GenerateRequirements(request);
            files["Dockerfile"] = GenerateDockerfile(request);
            files[".dockerignore"] = GenerateDockerIgnore();

            return Task.FromResult(files);
        }

        private string GenerateMainPy(TemplateGenerationRequest request)
        {
            var framework = request.Application?.Framework?.ToLower() ?? "fastapi";
            var sb = new StringBuilder();

            if (framework.Contains("fastapi"))
            {
                sb.AppendLine("from fastapi import FastAPI");
                sb.AppendLine("import uvicorn");
                sb.AppendLine();
                sb.AppendLine("app = FastAPI(");
                sb.AppendLine($"    title=\"{request.ServiceName}\",");
                sb.AppendLine($"    description=\"{request.Description}\"");
                sb.AppendLine(")");
                sb.AppendLine();
                sb.AppendLine("@app.get(\"/\")");
                sb.AppendLine("async def root():");
                sb.AppendLine($"    return {{\"message\": \"Welcome to {request.ServiceName}\"}}");
                sb.AppendLine();

                if (request.Application?.IncludeHealthCheck == true)
                {
                    sb.AppendLine("@app.get(\"/health\")");
                    sb.AppendLine("async def health():");
                    sb.AppendLine("    return {\"status\": \"healthy\"}");
                    sb.AppendLine();
                    sb.AppendLine("@app.get(\"/ready\")");
                    sb.AppendLine("async def ready():");
                    sb.AppendLine("    return {\"status\": \"ready\"}");
                    sb.AppendLine();
                }

                sb.AppendLine("if __name__ == \"__main__\":");
                sb.AppendLine($"    uvicorn.run(app, host=\"0.0.0.0\", port={request.Application?.Port ?? 8000})");
            }

            return sb.ToString();
        }

        private string GenerateRequirements(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            var framework = request.Application?.Framework?.ToLower() ?? "fastapi";

            if (framework.Contains("fastapi"))
            {
                sb.AppendLine("fastapi==0.109.0");
                sb.AppendLine("uvicorn[standard]==0.27.0");
                sb.AppendLine("pydantic==2.5.3");
            }
            else if (framework.Contains("flask"))
            {
                sb.AppendLine("flask==3.0.0");
            }

            foreach (var db in request.Databases)
            {
                var package = db.Type switch
                {
                    DatabaseType.PostgreSQL => "psycopg2-binary==2.9.9",
                    DatabaseType.MySQL => "mysql-connector-python==8.3.0",
                    DatabaseType.MongoDB => "pymongo==4.6.1",
                    DatabaseType.Redis => "redis==5.0.1",
                    _ => null
                };
                
                if (package != null)
                {
                    sb.AppendLine(package);
                }
            }

            if (request.Observability.ApplicationInsights)
            {
                sb.AppendLine("opencensus-ext-azure==1.1.13");
            }

            return sb.ToString();
        }

        private string GenerateDockerfile(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("FROM python:3.11-slim");
            sb.AppendLine();
            sb.AppendLine("WORKDIR /app");
            sb.AppendLine();
            sb.AppendLine("COPY requirements.txt .");
            sb.AppendLine("RUN pip install --no-cache-dir -r requirements.txt");
            sb.AppendLine();
            sb.AppendLine("COPY . .");
            sb.AppendLine();
            sb.AppendLine($"EXPOSE {request.Application?.Port ?? 8000}");
            sb.AppendLine();
            sb.AppendLine("CMD [\"python\", \"src/main.py\"]");
            return sb.ToString();
        }

        private string GenerateDockerIgnore()
        {
            return @"__pycache__
*.pyc
*.pyo
*.pyd
.Python
env/
venv/
.git
.gitignore
*.md
";
        }
    }

    /// <summary>
    /// Java application code generator
    /// </summary>
    public class JavaCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();

            files["src/main/java/com/example/Application.java"] = GenerateMainClass(request);
            files["pom.xml"] = GeneratePomXml(request);
            files["Dockerfile"] = GenerateDockerfile(request);
            files[".dockerignore"] = GenerateDockerIgnore();

            return Task.FromResult(files);
        }

        private string GenerateMainClass(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("package com.example;");
            sb.AppendLine();
            sb.AppendLine("import org.springframework.boot.SpringApplication;");
            sb.AppendLine("import org.springframework.boot.autoconfigure.SpringBootApplication;");
            sb.AppendLine("import org.springframework.web.bind.annotation.*;");
            sb.AppendLine();
            sb.AppendLine("@SpringBootApplication");
            sb.AppendLine("@RestController");
            sb.AppendLine("public class Application {");
            sb.AppendLine();
            sb.AppendLine("    public static void main(String[] args) {");
            sb.AppendLine("        SpringApplication.run(Application.class, args);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    @GetMapping(\"/\")");
            sb.AppendLine("    public String home() {");
            sb.AppendLine($"        return \"Welcome to {request.ServiceName}\";");
            sb.AppendLine("    }");
            
            if (request.Application?.IncludeHealthCheck == true)
            {
                sb.AppendLine();
                sb.AppendLine("    @GetMapping(\"/health\")");
                sb.AppendLine("    public String health() {");
                sb.AppendLine("        return \"{\\\"status\\\":\\\"healthy\\\"}\";");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GeneratePomXml(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<project xmlns=\"http://maven.apache.org/POM/4.0.0\"");
            sb.AppendLine("         xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
            sb.AppendLine("         xsi:schemaLocation=\"http://maven.apache.org/POM/4.0.0");
            sb.AppendLine("         http://maven.apache.org/xsd/maven-4.0.0.xsd\">");
            sb.AppendLine("    <modelVersion>4.0.0</modelVersion>");
            sb.AppendLine();
            sb.AppendLine("    <groupId>com.example</groupId>");
            sb.AppendLine($"    <artifactId>{request.ServiceName}</artifactId>");
            sb.AppendLine("    <version>1.0.0</version>");
            sb.AppendLine();
            sb.AppendLine("    <parent>");
            sb.AppendLine("        <groupId>org.springframework.boot</groupId>");
            sb.AppendLine("        <artifactId>spring-boot-starter-parent</artifactId>");
            sb.AppendLine("        <version>3.2.0</version>");
            sb.AppendLine("    </parent>");
            sb.AppendLine();
            sb.AppendLine("    <dependencies>");
            sb.AppendLine("        <dependency>");
            sb.AppendLine("            <groupId>org.springframework.boot</groupId>");
            sb.AppendLine("            <artifactId>spring-boot-starter-web</artifactId>");
            sb.AppendLine("        </dependency>");
            sb.AppendLine("    </dependencies>");
            sb.AppendLine("</project>");
            return sb.ToString();
        }

        private string GenerateDockerfile(TemplateGenerationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("FROM maven:3.9-eclipse-temurin-17 AS build");
            sb.AppendLine("WORKDIR /app");
            sb.AppendLine("COPY pom.xml .");
            sb.AppendLine("COPY src ./src");
            sb.AppendLine("RUN mvn clean package -DskipTests");
            sb.AppendLine();
            sb.AppendLine("FROM eclipse-temurin:17-jre");
            sb.AppendLine("WORKDIR /app");
            sb.AppendLine($"EXPOSE {request.Application?.Port ?? 8080}");
            sb.AppendLine("COPY --from=build /app/target/*.jar app.jar");
            sb.AppendLine("ENTRYPOINT [\"java\", \"-jar\", \"app.jar\"]");
            return sb.ToString();
        }

        private string GenerateDockerIgnore()
        {
            return @"target/
.mvn/
*.class
*.jar
.git
";
        }
    }

    // Stub implementations for other languages
    public class GoCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            files["src/main.go"] = "// Go implementation - to be completed";
            files["go.mod"] = $"module {request.ServiceName}\n\ngo 1.21";
            return Task.FromResult(files);
        }
    }

    public class RustCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            files["src/main.rs"] = "// Rust implementation - to be completed";
            files["Cargo.toml"] = $"[package]\nname = \"{request.ServiceName}\"\nversion = \"0.1.0\"\nedition = \"2021\"";
            return Task.FromResult(files);
        }
    }

    public class RubyCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            files["app.rb"] = "# Ruby implementation - to be completed";
            files["Gemfile"] = "source 'https://rubygems.org'\n\ngem 'sinatra'";
            return Task.FromResult(files);
        }
    }

    public class PHPCodeGenerator : IApplicationCodeGenerator
    {
        public Task<Dictionary<string, string>> GenerateAsync(TemplateGenerationRequest request, CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, string>();
            files["index.php"] = "<?php\n// PHP implementation - to be completed";
            files["composer.json"] = "{\n  \"require\": {}\n}";
            return Task.FromResult(files);
        }
    }
}
