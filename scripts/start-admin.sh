#!/bin/bash

# Start Admin API and Admin Client
echo "🚀 Starting Platform Admin System..."

# Check if Admin API is already running on port 5002
if lsof -Pi :5002 -sTCP:LISTEN -t >/dev/null ; then
    echo "✅ Admin API already running on port 5002"
else
    echo "📦 Starting Admin API on port 5002..."
    cd /Users/johnspinella/platform-mcp-supervisor/src/Platform.Engineering.Copilot.Admin
    nohup dotnet run > /tmp/admin-api.log 2>&1 &
    echo "   Admin API PID: $!"
    sleep 5
fi

# Check if Admin Client is already running on port 3001
if lsof -Pi :3001 -sTCP:LISTEN -t >/dev/null ; then
    echo "✅ Admin Client already running on port 3001"
else
    echo "🌐 Starting Admin Client on port 3001..."
    cd /Users/johnspinella/platform-mcp-supervisor/src/Platform.Engineering.Copilot.Admin.Client/ClientApp
    PORT=3001 npm start > /tmp/admin-client.log 2>&1 &
    echo "   Admin Client PID: $!"
    sleep 10
fi

echo ""
echo "==================================================="
echo "🎉 Platform Admin System Started!"
echo "==================================================="
echo "Admin API:    http://localhost:5050"
echo "Admin Client: http://localhost:5000"
echo "Swagger UI:   http://localhost:5050/swagger"
echo ""
echo "Logs:"
echo "  Admin API:    tail -f /tmp/admin-api.log"
echo "  Admin Client: tail -f /tmp/admin-client.log"
echo "==================================================="
