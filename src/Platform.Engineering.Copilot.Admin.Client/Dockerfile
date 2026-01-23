# syntax=docker/dockerfile:1.4
# Multi-architecture Dockerfile for Platform Engineering Copilot Admin Client (Blazor WASM)
# Supports: linux/amd64 (Azure ACI) and linux/arm64 (Apple Silicon local dev)

# Stage 1: Build Blazor WebAssembly app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files only (for better layer caching)
COPY ["Platform.Engineering.Copilot.sln", "./"]
COPY ["src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj", "src/Platform.Engineering.Copilot.Admin.Client/"]

# Restore dependencies
RUN dotnet restore "src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/src/Platform.Engineering.Copilot.Admin.Client"
RUN dotnet publish "Platform.Engineering.Copilot.Admin.Client.csproj" \
    -c Release \
    -o /app/publish \
    /p:BlazorEnableCompression=false

# Stage 2: Serve with Nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Remove default nginx static content
RUN rm -rf ./*

# Copy published Blazor WASM app
COPY --from=build /app/publish/wwwroot .

# Copy custom nginx config for Blazor WASM
COPY src/Platform.Engineering.Copilot.Admin.Client/nginx.conf /etc/nginx/nginx.conf

# Expose port 80
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost/health || exit 1

CMD ["nginx", "-g", "daemon off;"]
