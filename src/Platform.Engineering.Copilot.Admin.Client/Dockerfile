# syntax=docker/dockerfile:1.4
# Stage 1: Restore dependencies (cached layer)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore

# Install Node.js and npm using official binary distribution (cached layer)
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl xz-utils && \
    curl -fsSL https://nodejs.org/dist/v20.18.1/node-v20.18.1-linux-x64.tar.xz -o node.tar.xz && \
    tar -xf node.tar.xz -C /usr/local --strip-components=1 && \
    rm node.tar.xz && \
    apt-get remove -y curl xz-utils && \
    apt-get autoremove -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Verify installation
RUN node --version && npm --version

WORKDIR /src

# Copy solution file and all project files for restore
COPY Platform.Engineering.Copilot.sln ./
COPY src/Platform.Engineering.Copilot.Admin.Client/*.csproj ./src/Platform.Engineering.Copilot.Admin.Client/
COPY src/Platform.Engineering.Copilot.Core/*.csproj ./src/Platform.Engineering.Copilot.Core/

# Restore dependencies with BuildKit cache mount
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj \
    --runtime linux-x64

# Stage 2: Build
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY src/Platform.Engineering.Copilot.Admin.Client/. ./src/Platform.Engineering.Copilot.Admin.Client/
COPY src/Platform.Engineering.Copilot.Core/. ./src/Platform.Engineering.Copilot.Core/

# Build React frontend
WORKDIR /src/src/Platform.Engineering.Copilot.Admin.Client/ClientApp
RUN --mount=type=cache,target=/root/.npm \
    npm install && \
    npm run build

# Build and publish .NET backend with optimizations
WORKDIR /src/src/Platform.Engineering.Copilot.Admin.Client
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish Platform.Engineering.Copilot.Admin.Client.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    /p:PublishReadyToRun=false \
    /p:PublishTrimmed=false

# Stage 3: Runtime (optimized and hardened)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthchecks
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r -g 1000 appuser && \
    useradd -r -u 1000 -g appuser -m -s /sbin/nologin appuser && \
    chown -R appuser:appuser /app

# Copy published files with correct permissions
COPY --from=build --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5003

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5003 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5003/health || exit 1

ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Admin.Client.dll"]
