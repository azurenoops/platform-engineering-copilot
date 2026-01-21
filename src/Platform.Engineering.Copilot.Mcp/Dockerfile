# syntax=docker/dockerfile:1.4
# Multi-architecture Dockerfile for Platform Engineering Copilot MCP Server
# Supports: linux/amd64 (Azure ACI) and linux/arm64 (Apple Silicon local dev)

# Stage 1: Restore dependencies (cached layer)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy solution and project files only (for better layer caching)
# New Agent FX architecture: Core, Agents, State, Channels
COPY ["Platform.Engineering.Copilot.sln", "./"]
COPY ["src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj", "src/Platform.Engineering.Copilot.Mcp/"]
COPY ["src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj", "src/Platform.Engineering.Copilot.Core/"]
COPY ["src/Platform.Engineering.Copilot.Agents/Platform.Engineering.Copilot.Agents.csproj", "src/Platform.Engineering.Copilot.Agents/"]
COPY ["src/Platform.Engineering.Copilot.State/Platform.Engineering.Copilot.State.csproj", "src/Platform.Engineering.Copilot.State/"]
COPY ["src/Platform.Engineering.Copilot.Channels/Platform.Engineering.Copilot.Channels.csproj", "src/Platform.Engineering.Copilot.Channels/"]
COPY ["src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj", "src/Platform.Engineering.Copilot.Chat/"]

# Restore dependencies (architecture-independent)
RUN dotnet restore "src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj"

# Stage 2: Build
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY . .

# Build with optimizations (architecture-independent, uses container's native arch)
WORKDIR "/src/src/Platform.Engineering.Copilot.Mcp"
RUN dotnet build "Platform.Engineering.Copilot.Mcp.csproj" \
    -c Release \
    -o /app/build \
    --no-restore \
    /p:PublishReadyToRun=false \
    /p:PublishSingleFile=false

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "Platform.Engineering.Copilot.Mcp.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=false \
    /p:PublishTrimmed=false

# Stage 4: Runtime (optimized and hardened)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install dependencies including Azure CLI for Bicep compilation
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        apt-transport-https \
        lsb-release \
        gnupg && \
    # Install Azure CLI
    curl -sL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /usr/share/keyrings/microsoft-archive-keyring.gpg && \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-archive-keyring.gpg] https://packages.microsoft.com/repos/azure-cli/ $(lsb_release -cs) main" > /etc/apt/sources.list.d/azure-cli.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends azure-cli && \
    # Install Bicep system-wide (download binary directly to avoid user home dir issues)
    curl -Lo /usr/local/bin/bicep https://github.com/Azure/bicep/releases/latest/download/bicep-linux-x64 && \
    chmod +x /usr/local/bin/bicep && \
    # Also create symlink for 'az bicep' to find it
    mkdir -p /usr/local/share/az/bicep && \
    ln -s /usr/local/bin/bicep /usr/local/share/az/bicep/bicep && \
    # Cleanup
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Create Azure config directory for all users so az bicep can work
ENV AZURE_CONFIG_DIR=/opt/azure
RUN mkdir -p /opt/azure/bin && \
    ln -s /usr/local/bin/bicep /opt/azure/bin/bicep && \
    chmod -R 777 /opt/azure

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
