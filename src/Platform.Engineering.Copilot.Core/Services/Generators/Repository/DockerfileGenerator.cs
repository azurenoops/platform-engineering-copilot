using System.Text;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Repository;

/// <summary>
/// Generates optimized, multi-stage Dockerfiles for different programming languages
/// </summary>
public class DockerfileGenerator
{
    public Dictionary<string, string> GenerateDockerFiles(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        
        var app = request.Application ?? new ApplicationSpec();
        
        files["Dockerfile"] = GenerateDockerfile(request);
        files[".dockerignore"] = GenerateDockerIgnore(app.Language);
        files["docker-compose.yml"] = GenerateDockerCompose(request);
        files["docker-compose.dev.yml"] = GenerateDockerComposeDev(request);
        
        return files;
    }
    
    private string GenerateDockerfile(TemplateGenerationRequest request)
    {
        var app = request.Application ?? new ApplicationSpec();
        
        return app.Language switch
        {
            ProgrammingLanguage.NodeJS => GenerateNodeJSDockerfile(request),
            ProgrammingLanguage.Python => GeneratePythonDockerfile(request),
            ProgrammingLanguage.DotNet => GenerateDotNetDockerfile(request),
            ProgrammingLanguage.Java => GenerateJavaDockerfile(request),
            ProgrammingLanguage.Go => GenerateGoDockerfile(request),
            ProgrammingLanguage.Rust => GenerateRustDockerfile(request),
            ProgrammingLanguage.Ruby => GenerateRubyDockerfile(request),
            ProgrammingLanguage.PHP => GeneratePHPDockerfile(request),
            _ => GenerateGenericDockerfile(request)
        };
    }
    
    private string GenerateNodeJSDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 3000;
        
        sb.AppendLine("# Node.js Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM node:20-alpine AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy package files");
        sb.AppendLine("COPY package*.json ./");
        sb.AppendLine();
        sb.AppendLine("# Install dependencies");
        sb.AppendLine("RUN npm ci --only=production");
        sb.AppendLine();
        sb.AppendLine("# Copy source code");
        sb.AppendLine("COPY . .");
        sb.AppendLine();
        sb.AppendLine("# Build application (if needed)");
        sb.AppendLine("RUN if [ -f \"package.json\" ] && grep -q '\"build\"' package.json; then npm run build; fi");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM node:20-alpine");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN addgroup -g 1001 -S nodejs && adduser -S nodejs -u 1001");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy dependencies and built application");
        sb.AppendLine("COPY --from=builder --chown=nodejs:nodejs /app/node_modules ./node_modules");
        sb.AppendLine("COPY --from=builder --chown=nodejs:nodejs /app/dist ./dist");
        sb.AppendLine("COPY --from=builder --chown=nodejs:nodejs /app/package*.json ./");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER nodejs");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("# Health check");
        sb.AppendLine($"HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \\");
        sb.AppendLine($"  CMD node -e \"require('http').get('http://localhost:{port}/health', (r) => {{ process.exit(r.statusCode === 200 ? 0 : 1) }})\" || exit 1");
        sb.AppendLine();
        sb.AppendLine("CMD [\"node\", \"dist/index.js\"]");
        
        return sb.ToString();
    }
    
    private string GeneratePythonDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        var framework = app.Framework?.ToLower() ?? "fastapi";
        
        sb.AppendLine("# Python Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM python:3.11-slim AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Install system dependencies");
        sb.AppendLine("RUN apt-get update && apt-get install -y --no-install-recommends \\");
        sb.AppendLine("    gcc \\");
        sb.AppendLine("    && rm -rf /var/lib/apt/lists/*");
        sb.AppendLine();
        sb.AppendLine("# Copy requirements");
        sb.AppendLine("COPY requirements.txt .");
        sb.AppendLine();
        sb.AppendLine("# Install Python dependencies");
        sb.AppendLine("RUN pip install --no-cache-dir --user -r requirements.txt");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM python:3.11-slim");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN useradd -m -u 1001 appuser");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy Python packages from builder");
        sb.AppendLine("COPY --from=builder --chown=appuser:appuser /root/.local /home/appuser/.local");
        sb.AppendLine();
        sb.AppendLine("# Copy application code");
        sb.AppendLine("COPY --chown=appuser:appuser . .");
        sb.AppendLine();
        sb.AppendLine("# Update PATH");
        sb.AppendLine("ENV PATH=/home/appuser/.local/bin:$PATH");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER appuser");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("# Health check");
        sb.AppendLine($"HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \\");
        sb.AppendLine($"  CMD curl -f http://localhost:{port}/health || exit 1");
        sb.AppendLine();
        
        if (framework.Contains("fastapi"))
        {
            sb.AppendLine($"CMD [\"uvicorn\", \"app.main:app\", \"--host\", \"0.0.0.0\", \"--port\", \"{port}\"]");
        }
        else if (framework.Contains("flask"))
        {
            sb.AppendLine($"CMD [\"gunicorn\", \"--bind\", \"0.0.0.0:{port}\", \"--workers\", \"4\", \"app:app\"]");
        }
        else if (framework.Contains("django"))
        {
            sb.AppendLine($"CMD [\"gunicorn\", \"--bind\", \"0.0.0.0:{port}\", \"--workers\", \"4\", \"config.wsgi:application\"]");
        }
        else
        {
            sb.AppendLine($"CMD [\"python\", \"app.py\"]");
        }
        
        return sb.ToString();
    }
    
    private string GenerateDotNetDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var serviceName = request.ServiceName ?? "MyApp";
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# .NET Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /src");
        sb.AppendLine();
        sb.AppendLine("# Copy csproj and restore dependencies");
        sb.AppendLine($"COPY *.csproj ./");
        sb.AppendLine("RUN dotnet restore");
        sb.AppendLine();
        sb.AppendLine("# Copy everything else and build");
        sb.AppendLine("COPY . ./");
        sb.AppendLine("RUN dotnet publish -c Release -o /app/publish --no-restore");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM mcr.microsoft.com/dotnet/aspnet:8.0");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN useradd -m -u 1001 appuser");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy published application");
        sb.AppendLine("COPY --from=builder --chown=appuser:appuser /app/publish .");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER appuser");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("# Health check");
        sb.AppendLine($"HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \\");
        sb.AppendLine($"  CMD curl -f http://localhost:{port}/health || exit 1");
        sb.AppendLine();
        sb.AppendLine($"ENTRYPOINT [\"dotnet\", \"{serviceName}.dll\"]");
        
        return sb.ToString();
    }
    
    private string GenerateJavaDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var serviceName = request.ServiceName ?? "app";
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# Java Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM maven:3.9-eclipse-temurin-17 AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy pom.xml and download dependencies");
        sb.AppendLine("COPY pom.xml .");
        sb.AppendLine("RUN mvn dependency:go-offline");
        sb.AppendLine();
        sb.AppendLine("# Copy source and build");
        sb.AppendLine("COPY src ./src");
        sb.AppendLine("RUN mvn package -DskipTests");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM eclipse-temurin:17-jre-alpine");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN addgroup -g 1001 -S appuser && adduser -S appuser -u 1001 -G appuser");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy JAR from builder");
        sb.AppendLine($"COPY --from=builder --chown=appuser:appuser /app/target/{serviceName}.jar ./app.jar");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER appuser");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("# Health check");
        sb.AppendLine($"HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \\");
        sb.AppendLine($"  CMD curl -f http://localhost:{port}/actuator/health || exit 1");
        sb.AppendLine();
        sb.AppendLine("ENTRYPOINT [\"java\", \"-jar\", \"app.jar\"]");
        
        return sb.ToString();
    }
    
    private string GenerateGoDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# Go Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM golang:1.21-alpine AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Install build dependencies");
        sb.AppendLine("RUN apk add --no-cache git");
        sb.AppendLine();
        sb.AppendLine("# Copy go mod files");
        sb.AppendLine("COPY go.mod go.sum ./");
        sb.AppendLine("RUN go mod download");
        sb.AppendLine();
        sb.AppendLine("# Copy source code");
        sb.AppendLine("COPY . .");
        sb.AppendLine();
        sb.AppendLine("# Build the application");
        sb.AppendLine("RUN CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o main .");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM alpine:latest");
        sb.AppendLine();
        sb.AppendLine("# Install CA certificates");
        sb.AppendLine("RUN apk --no-cache add ca-certificates");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN addgroup -g 1001 -S appuser && adduser -S appuser -u 1001 -G appuser");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy binary from builder");
        sb.AppendLine("COPY --from=builder --chown=appuser:appuser /app/main .");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER appuser");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("# Health check");
        sb.AppendLine($"HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \\");
        sb.AppendLine($"  CMD wget -q --spider http://localhost:{port}/health || exit 1");
        sb.AppendLine();
        sb.AppendLine("CMD [\"./main\"]");
        
        return sb.ToString();
    }
    
    private string GenerateRustDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# Rust Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM rust:1.75-alpine AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Install build dependencies");
        sb.AppendLine("RUN apk add --no-cache musl-dev");
        sb.AppendLine();
        sb.AppendLine("# Copy manifests");
        sb.AppendLine("COPY Cargo.toml Cargo.lock ./");
        sb.AppendLine();
        sb.AppendLine("# Copy source code");
        sb.AppendLine("COPY src ./src");
        sb.AppendLine();
        sb.AppendLine("# Build release binary");
        sb.AppendLine("RUN cargo build --release");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM alpine:latest");
        sb.AppendLine();
        sb.AppendLine("# Install runtime dependencies");
        sb.AppendLine("RUN apk --no-cache add ca-certificates");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN addgroup -g 1001 -S appuser && adduser -S appuser -u 1001 -G appuser");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy binary from builder");
        sb.AppendLine("COPY --from=builder --chown=appuser:appuser /app/target/release/app .");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER appuser");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("CMD [\"./app\"]");
        
        return sb.ToString();
    }
    
    private string GenerateRubyDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 3000;
        
        sb.AppendLine("# Ruby Multi-stage Docker Build");
        sb.AppendLine("# Build stage");
        sb.AppendLine("FROM ruby:3.2-alpine AS builder");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Install build dependencies");
        sb.AppendLine("RUN apk add --no-cache build-base postgresql-dev");
        sb.AppendLine();
        sb.AppendLine("# Copy Gemfile");
        sb.AppendLine("COPY Gemfile Gemfile.lock ./");
        sb.AppendLine();
        sb.AppendLine("# Install gems");
        sb.AppendLine("RUN bundle install --without development test");
        sb.AppendLine();
        sb.AppendLine("# Production stage");
        sb.AppendLine("FROM ruby:3.2-alpine");
        sb.AppendLine();
        sb.AppendLine("# Install runtime dependencies");
        sb.AppendLine("RUN apk add --no-cache postgresql-client");
        sb.AppendLine();
        sb.AppendLine("# Add non-root user");
        sb.AppendLine("RUN addgroup -g 1001 -S appuser && adduser -S appuser -u 1001 -G appuser");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine();
        sb.AppendLine("# Copy gems from builder");
        sb.AppendLine("COPY --from=builder --chown=appuser:appuser /usr/local/bundle /usr/local/bundle");
        sb.AppendLine();
        sb.AppendLine("# Copy application code");
        sb.AppendLine("COPY --chown=appuser:appuser . .");
        sb.AppendLine();
        sb.AppendLine("# Switch to non-root user");
        sb.AppendLine("USER appuser");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("CMD [\"ruby\", \"app.rb\"]");
        
        return sb.ToString();
    }
    
    private string GeneratePHPDockerfile(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("# PHP Multi-stage Docker Build");
        sb.AppendLine("FROM php:8.2-fpm-alpine");
        sb.AppendLine();
        sb.AppendLine("# Install dependencies");
        sb.AppendLine("RUN apk add --no-cache nginx");
        sb.AppendLine();
        sb.AppendLine("# Install PHP extensions");
        sb.AppendLine("RUN docker-php-ext-install pdo pdo_mysql");
        sb.AppendLine();
        sb.AppendLine("WORKDIR /var/www/html");
        sb.AppendLine();
        sb.AppendLine("# Copy application code");
        sb.AppendLine("COPY . .");
        sb.AppendLine();
        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();
        sb.AppendLine("CMD [\"php-fpm\"]");
        
        return sb.ToString();
    }
    
    private string GenerateGenericDockerfile(TemplateGenerationRequest request)
    {
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        
        return $@"# Generic Dockerfile
FROM alpine:latest

WORKDIR /app

COPY . .

EXPOSE {port}

CMD [""./app""]
";
    }
    
    private string GenerateDockerIgnore(ProgrammingLanguage language)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Git");
        sb.AppendLine(".git");
        sb.AppendLine(".gitignore");
        sb.AppendLine(".gitattributes");
        sb.AppendLine();
        sb.AppendLine("# CI/CD");
        sb.AppendLine(".github");
        sb.AppendLine(".gitlab-ci.yml");
        sb.AppendLine("azure-pipelines.yml");
        sb.AppendLine();
        sb.AppendLine("# Documentation");
        sb.AppendLine("README.md");
        sb.AppendLine("CONTRIBUTING.md");
        sb.AppendLine("LICENSE");
        sb.AppendLine("docs/");
        sb.AppendLine();
        sb.AppendLine("# IDE");
        sb.AppendLine(".vscode");
        sb.AppendLine(".idea");
        sb.AppendLine("*.swp");
        sb.AppendLine(".DS_Store");
        sb.AppendLine();
        
        switch (language)
        {
            case ProgrammingLanguage.NodeJS:
                sb.AppendLine("# Node.js");
                sb.AppendLine("node_modules");
                sb.AppendLine("npm-debug.log");
                sb.AppendLine("coverage");
                sb.AppendLine(".npm");
                break;
                
            case ProgrammingLanguage.Python:
                sb.AppendLine("# Python");
                sb.AppendLine("__pycache__");
                sb.AppendLine("*.pyc");
                sb.AppendLine("*.pyo");
                sb.AppendLine("*.pyd");
                sb.AppendLine(".pytest_cache");
                sb.AppendLine(".mypy_cache");
                sb.AppendLine("venv");
                sb.AppendLine("env");
                break;
                
            case ProgrammingLanguage.DotNet:
                sb.AppendLine("# .NET");
                sb.AppendLine("bin");
                sb.AppendLine("obj");
                sb.AppendLine("*.user");
                break;
                
            case ProgrammingLanguage.Java:
                sb.AppendLine("# Java");
                sb.AppendLine("target");
                sb.AppendLine(".gradle");
                sb.AppendLine("build");
                break;
        }
        
        sb.AppendLine();
        sb.AppendLine("# Tests");
        sb.AppendLine("tests/");
        sb.AppendLine("test/");
        sb.AppendLine("*.test.js");
        sb.AppendLine("*.spec.js");
        
        return sb.ToString();
    }
    
    private string GenerateDockerCompose(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "app";
        var app = request.Application ?? new ApplicationSpec();
        var port = app.Port > 0 ? app.Port : 8080;
        
        sb.AppendLine("version: '3.8'");
        sb.AppendLine();
        sb.AppendLine("services:");
        sb.AppendLine($"  {serviceName}:");
        sb.AppendLine("    build:");
        sb.AppendLine("      context: .");
        sb.AppendLine("      dockerfile: Dockerfile");
        sb.AppendLine("    ports:");
        sb.AppendLine($"      - \"{port}:{port}\"");
        sb.AppendLine("    environment:");
        sb.AppendLine("      - NODE_ENV=production");
        
        if (app.EnvironmentVariables != null && app.EnvironmentVariables.Any())
        {
            foreach (var env in app.EnvironmentVariables)
            {
                sb.AppendLine($"      - {env.Key}={env.Value}");
            }
        }
        
        sb.AppendLine("    restart: unless-stopped");
        sb.AppendLine("    networks:");
        sb.AppendLine("      - app-network");
        sb.AppendLine();
        
        // Add databases if configured (for local development)
        foreach (var db in request.Databases.Where(d => d.Location == DatabaseLocation.Kubernetes))
        {
            sb.AppendLine($"  {db.Name.ToLower()}:");
            
            switch (db.Type)
            {
                case DatabaseType.PostgreSQL:
                    sb.AppendLine($"    image: postgres:{db.Version ?? "15"}-alpine");
                    sb.AppendLine("    environment:");
                    sb.AppendLine("      - POSTGRES_DB=appdb");
                    sb.AppendLine("      - POSTGRES_USER=appuser");
                    sb.AppendLine("      - POSTGRES_PASSWORD=changeme");
                    break;
                    
                case DatabaseType.MySQL:
                    sb.AppendLine($"    image: mysql:{db.Version ?? "8"}");
                    sb.AppendLine("    environment:");
                    sb.AppendLine("      - MYSQL_DATABASE=appdb");
                    sb.AppendLine("      - MYSQL_USER=appuser");
                    sb.AppendLine("      - MYSQL_PASSWORD=changeme");
                    sb.AppendLine("      - MYSQL_ROOT_PASSWORD=changeme");
                    break;
                    
                case DatabaseType.MongoDB:
                    sb.AppendLine($"    image: mongo:{db.Version ?? "7"}");
                    sb.AppendLine("    environment:");
                    sb.AppendLine("      - MONGO_INITDB_DATABASE=appdb");
                    break;
                    
                case DatabaseType.Redis:
                    sb.AppendLine($"    image: redis:{db.Version ?? "7"}-alpine");
                    break;
            }
            
            sb.AppendLine("    volumes:");
            sb.AppendLine($"      - {db.Name.ToLower()}-data:/var/lib/{GetDatabaseDataPath(db.Type)}");
            sb.AppendLine("    networks:");
            sb.AppendLine("      - app-network");
            sb.AppendLine();
        }
        
        sb.AppendLine("networks:");
        sb.AppendLine("  app-network:");
        sb.AppendLine("    driver: bridge");
        sb.AppendLine();
        sb.AppendLine("volumes:");
        
        foreach (var db in request.Databases.Where(d => d.Location == DatabaseLocation.Kubernetes))
        {
            sb.AppendLine($"  {db.Name.ToLower()}-data:");
        }
        
        return sb.ToString();
    }
    
    private string GenerateDockerComposeDev(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "app";
        var app = request.Application ?? new ApplicationSpec();
        
        sb.AppendLine("version: '3.8'");
        sb.AppendLine();
        sb.AppendLine("services:");
        sb.AppendLine($"  {serviceName}:");
        sb.AppendLine("    build:");
        sb.AppendLine("      context: .");
        sb.AppendLine("      dockerfile: Dockerfile");
        sb.AppendLine("    volumes:");
        sb.AppendLine("      - .:/app");
        
        if (app.Language == ProgrammingLanguage.NodeJS)
        {
            sb.AppendLine("      - /app/node_modules");
        }
        
        sb.AppendLine("    environment:");
        sb.AppendLine("      - NODE_ENV=development");
        sb.AppendLine("      - DEBUG=*");
        sb.AppendLine("    command: npm run dev");
        
        return sb.ToString();
    }
    
    private string GetDatabaseDataPath(DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => "postgresql/data",
            DatabaseType.MySQL => "mysql",
            DatabaseType.MongoDB => "mongodb",
            DatabaseType.Redis => "redis",
            _ => "data"
        };
    }
}
