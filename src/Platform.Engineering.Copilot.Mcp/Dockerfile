# syntax=docker/dockerfile:1.4
# Stage 1: Restore dependencies (cached layer)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy solution and project files only (for better layer caching)
COPY ["Platform.Engineering.Copilot.sln", "./"]
COPY ["src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj", "src/Platform.Engineering.Copilot.Mcp/"]
COPY ["src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj", "src/Platform.Engineering.Copilot.Core/"]
COPY ["src/Platform.Engineering.Copilot.Compliance.Agent/Platform.Engineering.Copilot.Compliance.Agent.csproj", "src/Platform.Engineering.Copilot.Compliance.Agent/"]
COPY ["src/Platform.Engineering.Copilot.Infrastructure.Agent/Platform.Engineering.Copilot.Infrastructure.Agent.csproj", "src/Platform.Engineering.Copilot.Infrastructure.Agent/"]
COPY ["src/Platform.Engineering.Copilot.CostManagement.Agent/Platform.Engineering.Copilot.CostManagement.Agent.csproj", "src/Platform.Engineering.Copilot.CostManagement.Agent/"]
COPY ["src/Platform.Engineering.Copilot.Security.Agent/Platform.Engineering.Copilot.Security.Agent.csproj", "src/Platform.Engineering.Copilot.Security.Agent/"]
COPY ["src/Platform.Engineering.Copilot.Environment.Agent/Platform.Engineering.Copilot.Environment.Agent.csproj", "src/Platform.Engineering.Copilot.Environment.Agent/"]
COPY ["src/Platform.Engineering.Copilot.Discovery.Agent/Platform.Engineering.Copilot.Discovery.Agent.csproj", "src/Platform.Engineering.Copilot.Discovery.Agent/"]
COPY ["src/Platform.Engineering.Copilot.KnowledgeBase.Agent/Platform.Engineering.Copilot.KnowledgeBase.Agent.csproj", "src/Platform.Engineering.Copilot.KnowledgeBase.Agent/"]

# Restore with BuildKit cache mount for NuGet packages
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj" \
    --runtime linux-x64

# Stage 2: Build
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY . .

# Build with optimizations
WORKDIR "/src/src/Platform.Engineering.Copilot.Mcp"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build "Platform.Engineering.Copilot.Mcp.csproj" \
    -c Release \
    -o /app/build \
    --no-restore \
    --runtime linux-x64 \
    /p:PublishReadyToRun=false \
    /p:PublishSingleFile=false

# Stage 3: Publish
FROM build AS publish
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "Platform.Engineering.Copilot.Mcp.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=false \
    /p:PublishTrimmed=false

# Stage 4: Runtime (optimized and hardened)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install Node.js for Azure MCP Server (using non-root install)
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        xz-utils && \
    curl -fsSL https://nodejs.org/dist/v20.18.1/node-v20.18.1-linux-x64.tar.xz -o node.tar.xz && \
    tar -xf node.tar.xz -C /usr/local --strip-components=1 && \
    rm node.tar.xz && \
    apt-get remove -y xz-utils && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Verify installations
RUN node --version && npm --version

# Create non-root user for security
RUN groupadd -r -g 1000 appuser && \
    useradd -r -u 1000 -g appuser -m -s /sbin/nologin appuser && \
    chown -R appuser:appuser /app

# Copy published app with correct permissions
COPY --from=publish --chown=appuser:appuser /app/publish .

# Copy appsettings.json from repository root
COPY --chown=appuser:appuser appsettings.json ./appsettings.json

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5100

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5100 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5100/health || exit 1

# Entry point - runs in HTTP mode by default for container deployments
# For stdio mode (GitHub Copilot/Claude), run without --http flag
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Mcp.dll", "--http"]
