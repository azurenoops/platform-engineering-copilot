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
COPY src/Platform.Engineering.Copilot.Chat/*.csproj ./src/Platform.Engineering.Copilot.Chat/
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
    dotnet restore ./src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj \
    --runtime linux-x64

# Stage 2: Build
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY src/Platform.Engineering.Copilot.Chat/. ./src/Platform.Engineering.Copilot.Chat/
COPY src/Platform.Engineering.Copilot.Core/. ./src/Platform.Engineering.Copilot.Core/
COPY src/Platform.Engineering.Copilot.Compliance.Agent/. ./src/Platform.Engineering.Copilot.Compliance.Agent/
COPY src/Platform.Engineering.Copilot.Infrastructure.Agent/. ./src/Platform.Engineering.Copilot.Infrastructure.Agent/
COPY src/Platform.Engineering.Copilot.CostManagement.Agent/. ./src/Platform.Engineering.Copilot.CostManagement.Agent/
COPY src/Platform.Engineering.Copilot.Environment.Agent/. ./src/Platform.Engineering.Copilot.Environment.Agent/
COPY src/Platform.Engineering.Copilot.Discovery.Agent/. ./src/Platform.Engineering.Copilot.Discovery.Agent/
COPY src/Platform.Engineering.Copilot.Security.Agent/. ./src/Platform.Engineering.Copilot.Security.Agent/
COPY src/Platform.Engineering.Copilot.KnowledgeBase.Agent/. ./src/Platform.Engineering.Copilot.KnowledgeBase.Agent/

# Build and publish with optimizations
WORKDIR /src/src/Platform.Engineering.Copilot.Chat
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish Platform.Engineering.Copilot.Chat.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --runtime linux-x64 \
    /p:PublishReadyToRun=false \
    /p:PublishTrimmed=false \
    /p:SkipSpaPublish=true

# Stage 3: Runtime (optimized and hardened)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
USER root
RUN groupadd -r -g 1000 appuser && \
    useradd -r -u 1000 -g appuser -m -s /sbin/nologin appuser && \
    chown -R appuser:appuser /app

# Copy published files with correct permissions
COPY --from=build --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5001

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5001/health || exit 1

ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Chat.dll"]
