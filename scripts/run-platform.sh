#!/bin/bash

# Platform Engineering Chat - Quick Start Script
echo "🚀 Starting Platform Engineering Chat Application..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker Desktop first."
    exit 1
fi

# Start SQL Server container
echo "📦 Starting SQL Server container..."
docker-compose up -d sqlserver

# Wait for SQL Server to be healthy
echo "⏳ Waiting for SQL Server to be ready..."
until docker exec supervisor-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P SupervisorDB123! -Q "SELECT 1" > /dev/null 2>&1; do
    echo "   Waiting for SQL Server..."
    sleep 2
done

echo "✅ SQL Server is ready!"

# Start the Platform Chat application
echo "🌐 Starting Platform Chat application..."
cd src/Platform.Engineering.Copilot.Chat
dotnet run

echo "🎉 Platform Chat application is starting at http://localhost:3000"