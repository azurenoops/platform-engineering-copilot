# syntax=docker/dockerfile:1.4
# Multi-architecture Dockerfile for Platform Engineering Copilot Admin API
# Supports: linux/amd64 (Azure ACI) and linux/arm64 (Apple Silicon local dev)

# Stage 1: Restore dependencies (cached layer)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy solution and project files only (for better layer caching)
COPY ["Platform.Engineering.Copilot.sln", "./"]
COPY ["src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj", "src/Platform.Engineering.Copilot.Admin.API/"]
COPY ["src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj", "src/Platform.Engineering.Copilot.Core/"]
COPY ["src/Platform.Engineering.Copilot.Agents/Platform.Engineering.Copilot.Agents.csproj", "src/Platform.Engineering.Copilot.Agents/"]
COPY ["src/Platform.Engineering.Copilot.State/Platform.Engineering.Copilot.State.csproj", "src/Platform.Engineering.Copilot.State/"]
COPY ["src/Platform.Engineering.Copilot.Channels/Platform.Engineering.Copilot.Channels.csproj", "src/Platform.Engineering.Copilot.Channels/"]

# Restore dependencies
RUN dotnet restore "src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj"

# Stage 2: Build
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY . .

# Build with optimizations
WORKDIR "/src/src/Platform.Engineering.Copilot.Admin.API"
RUN dotnet build "Platform.Engineering.Copilot.Admin.API.csproj" \
    -c Release \
    -o /app/build \
    --no-restore \
    /p:PublishReadyToRun=false \
    /p:PublishSingleFile=false

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "Platform.Engineering.Copilot.Admin.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=false \
    /p:PublishTrimmed=false

# Stage 4: Runtime
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
RUN groupadd --gid 1000 appgroup && \
    useradd --uid 1000 --gid appgroup --shell /bin/bash --create-home appuser

# Copy published application
COPY --from=publish /app/publish .

# Set permissions
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Environment
ENV ASPNETCORE_URLS=http://+:5050
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 5050

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5050/health || exit 1

ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Admin.API.dll"]
