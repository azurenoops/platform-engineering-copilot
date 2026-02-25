# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY Platform.Engineering.Copilot.sln .
COPY src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj src/Platform.Engineering.Copilot.Admin.Client/

RUN dotnet restore src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj

# Copy source code and publish
COPY src/Platform.Engineering.Copilot.Admin.Client/ src/Platform.Engineering.Copilot.Admin.Client/
RUN dotnet publish src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: Serve with nginx
FROM nginx:alpine AS runtime
EXPOSE 80

# Copy published Blazor WASM output to nginx
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Custom nginx config for SPA routing
COPY src/Platform.Engineering.Copilot.Admin.Client/nginx.conf /etc/nginx/conf.d/default.conf
