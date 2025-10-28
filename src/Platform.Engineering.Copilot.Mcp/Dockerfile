# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Platform.Engineering.Copilot.sln", "./"]
COPY ["src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj", "src/Platform.Engineering.Copilot.Mcp/"]
COPY ["src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj", "src/Platform.Engineering.Copilot.Core/"]

# Restore dependencies
RUN dotnet restore "src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/Platform.Engineering.Copilot.Mcp"
RUN dotnet build "Platform.Engineering.Copilot.Mcp.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "Platform.Engineering.Copilot.Mcp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5100

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5100
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Entry point - runs in HTTP mode by default for container deployments
# For stdio mode (GitHub Copilot/Claude), run without --http flag
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Mcp.dll", "--http"]
