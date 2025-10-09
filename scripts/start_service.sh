#!/bin/bash

# Platform MCP Service Startup Script

echo "🚀 Starting Platform Engineering MCP Server..."
echo ""

# Check if already running
if curl -s --max-time 2 http://localhost:8080/health > /dev/null 2>&1; then
    echo "✅ Service is already running on port 8080"
    echo "🎯 You can now run ATO scans with: ./run_ato_scan.sh"
    exit 0
fi

echo "📁 Checking configuration..."

# Ensure appsettings.json is in the root directory
if [ ! -f "appsettings.json" ]; then
    echo "📋 Copying appsettings.json to root directory..."
    cp src/Platform.Engineering.Copilot.Platform/appsettings.json . 2>/dev/null || echo "⚠️  Could not copy appsettings.json"
fi

echo "🔧 Starting the service..."
echo "💡 This will run in the background. Press Ctrl+C to stop when needed."
echo ""

# Start the service
cd /Users/johnspinella/platform-mcp-supervisor
dotnet run --project ./src/Platform.Engineering.Copilot.Platform/Platform.Engineering.Copilot.csproj