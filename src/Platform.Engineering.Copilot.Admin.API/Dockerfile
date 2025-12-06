# syntax=docker/dockerfile:1.4
# Stage 1: Restore dependencies (cached layer)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy solution file and all project files for restore
COPY Platform.Engineering.Copilot.sln ./
COPY src/Platform.Engineering.Copilot.Admin.API/*.csproj ./src/Platform.Engineering.Copilot.Admin.API/
COPY src/Platform.Engineering.Copilot.Core/*.csproj ./src/Platform.Engineering.Copilot.Core/
COPY src/Platform.Engineering.Copilot.Compliance.Agent/*.csproj ./src/Platform.Engineering.Copilot.Compliance.Agent/
COPY src/Platform.Engineering.Copilot.Infrastructure.Agent/*.csproj ./src/Platform.Engineering.Copilot.Infrastructure.Agent/
COPY src/Platform.Engineering.Copilot.CostManagement.Agent/*.csproj ./src/Platform.Engineering.Copilot.CostManagement.Agent/
COPY src/Platform.Engineering.Copilot.Environment.Agent/*.csproj ./src/Platform.Engineering.Copilot.Environment.Agent/
COPY src/Platform.Engineering.Copilot.Discovery.Agent/*.csproj ./src/Platform.Engineering.Copilot.Discovery.Agent/
COPY src/Platform.Engineering.Copilot.Security.Agent/*.csproj ./src/Platform.Engineering.Copilot.Security.Agent/
COPY src/Platform.Engineering.Copilot.KnowledgeBase.Agent/*.csproj ./src/Platform.Engineering.Copilot.KnowledgeBase.Agent/

# Restore dependencies with BuildKit cache mount
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj \
    --runtime linux-x64

# Stage 2: Build
FROM restore AS build
WORKDIR /src

# Copy root appsettings.json (contains AgentConfiguration section)
COPY appsettings.json ./

# Copy all source code
COPY src/Platform.Engineering.Copilot.Admin.API/. ./src/Platform.Engineering.Copilot.Admin.API/
COPY src/Platform.Engineering.Copilot.Core/. ./src/Platform.Engineering.Copilot.Core/
COPY src/Platform.Engineering.Copilot.Compliance.Agent/. ./src/Platform.Engineering.Copilot.Compliance.Agent/
COPY src/Platform.Engineering.Copilot.Infrastructure.Agent/. ./src/Platform.Engineering.Copilot.Infrastructure.Agent/
COPY src/Platform.Engineering.Copilot.CostManagement.Agent/. ./src/Platform.Engineering.Copilot.CostManagement.Agent/
COPY src/Platform.Engineering.Copilot.Environment.Agent/. ./src/Platform.Engineering.Copilot.Environment.Agent/
COPY src/Platform.Engineering.Copilot.Discovery.Agent/. ./src/Platform.Engineering.Copilot.Discovery.Agent/
COPY src/Platform.Engineering.Copilot.Security.Agent/. ./src/Platform.Engineering.Copilot.Security.Agent/
COPY src/Platform.Engineering.Copilot.KnowledgeBase.Agent/. ./src/Platform.Engineering.Copilot.KnowledgeBase.Agent/

# Build and publish with optimizations
WORKDIR /src/src/Platform.Engineering.Copilot.Admin.API
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish Platform.Engineering.Copilot.Admin.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    /p:PublishReadyToRun=false \
    /p:PublishTrimmed=false

# Stage 3: Runtime (optimized and hardened)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthcheck (minimal layer)
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Create non-root user for security
RUN groupadd -r -g 1000 appuser && \
    useradd -r -u 1000 -g appuser -m -s /sbin/nologin appuser && \
    chown -R appuser:appuser /app

# Copy published files with correct permissions
COPY --from=build --chown=appuser:appuser /app/publish .

# Copy root appsettings.json to root of container (for Program.cs path resolution)
COPY --from=build --chown=appuser:appuser /src/appsettings.json /appsettings.json

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5002

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5002 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5002/health || exit 1

ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Admin.API.dll"]
