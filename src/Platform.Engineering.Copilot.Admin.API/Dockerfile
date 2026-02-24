# Stage 1: Build (multi-arch)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy solution and project files for layer caching
COPY Platform.Engineering.Copilot.sln .
COPY src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj src/Platform.Engineering.Copilot.Core/
COPY src/Platform.Engineering.Copilot.Agents/Platform.Engineering.Copilot.Agents.csproj src/Platform.Engineering.Copilot.Agents/
COPY src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj src/Platform.Engineering.Copilot.Admin.API/
COPY src/Platform.Engineering.Copilot.State/Platform.Engineering.Copilot.State.csproj src/Platform.Engineering.Copilot.State/
COPY src/Platform.Engineering.Copilot.Channels/Platform.Engineering.Copilot.Channels.csproj src/Platform.Engineering.Copilot.Channels/

RUN dotnet restore src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj

# Copy source code and build
COPY src/ src/
RUN dotnet publish src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj \
    -c Release -o /app/publish --no-restore -a $TARGETARCH

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 5050

ENV ASPNETCORE_URLS=http://+:5050

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Admin.API.dll"]
