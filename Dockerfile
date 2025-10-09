# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln .
COPY src/ src/
COPY tests/ tests/

# Restore dependencies
RUN dotnet restore

# Build the solution
RUN dotnet build -c Release --no-restore

# Publish Platform API
RUN dotnet publish src/Platform.Engineering.Copilot.API/Platform.Engineering.Copilot.API.csproj \
    -c Release -o /app/publish/api --no-build

# Runtime stage for Platform API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-runtime
WORKDIR /app
COPY --from=build /app/publish/api .
EXPOSE 5000
EXPOSE 5001
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.API.dll"]
